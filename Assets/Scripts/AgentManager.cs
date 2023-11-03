using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.UIElements;
using UnityEngine;

public class AgentManager : MonoBehaviour
{

    [Header("Agent Settings")]
    public float AgentAverageSpeed = 15.0f;
    public float AgentMaxSpeed = 100.0f;

    [Header("Population Settings")]
    public int PopulationStartSize = 10;
    public int MinPopulationSize = 10;
    public int MaxPopulationSize = 30;
    public int NewGenerationSizeOffset = 5;
    public int MaxGenerations = 100;

    [Header("Simulation Time Settings")]
    public float TimeStep = 0.5f;
    public float GenerationMaxTime = 15.0f;
    public float GenerationTimeIncrement = 0.5f;

    [Header("Selection Settings")]
    public int SelectionSize = 10;
    public int TournamentSize = 5;

    [Header("Mutation Settings")]
    public float MutationRate = 0.01f;
    public float MutationRateIncrement = 0.02f;
    public float SimilarFitnessThreshold = 3f;

    [Header("Fitness Function Settings")]
    public float DeathPenalty = 100.0f;
    public float DistanceReward = 1.0f;
    public float LifeTimeReward = 1.0f;
    public float ExplorationReward = 10.0f;
    public float GoalReward = 100.0f;

    [Header("Exploration Settings")]
    public float CellSize = 5.0f;

    private List<Agent> Population;
    
    private Dictionary<Agent, float> FitnessValues;

    private GameObject Goal;

    private int CompletedAgents = 0;
    private int CurrentGen = 0;



    int nGenSimilarFitness = 0;

    Vector3 offset = Vector3.zero;

    Dictionary<int, Dictionary<int, float>> MazeValues;

    Dictionary<Vector2Int, GameObject> mazeCells;

    MazeGenerator mazeGenerator;

    bool[,] MazeDeadEnds;
    Dictionary<Agent, bool[,]> MazeFlaggedPath;

    private void Start()
    {

        Goal = GameObject.FindGameObjectWithTag("Goal");
        Population = new List<Agent>();
        FitnessValues = new Dictionary<Agent, float>();
        MazeValues = new Dictionary<int, Dictionary<int, float>>();
        mazeCells = new Dictionary<Vector2Int, GameObject>();

        Agent.TimeStep = TimeStep;
        Agent.GenerationTime = GenerationMaxTime;
        Agent.Goal = Goal;
        Agent.AverageSpeed = AgentAverageSpeed;
        Agent.MaxSpeed = AgentMaxSpeed;

        

        mazeGenerator = GameObject.FindObjectOfType<MazeGenerator>();
        var maze = mazeGenerator.GenerateMaze();
        
        var startCell = maze[0, 0];
        var endCell = mazeGenerator.LastMazeCell;

        this.transform.position = new Vector3(startCell.transform.position.x, 1.2f, startCell.transform.position.z);
        Goal.transform.position = new Vector3(endCell.transform.position.x, 1.2f, endCell.transform.position.z);

        MazeDeadEnds = new bool[maze.GetLength(0), maze.GetLength(1)];
        MazeFlaggedPath = new Dictionary<Agent, bool[,]>();

        StartCoroutine(GenerationsRoutine());
    }

    public static float ManhattanDistance(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private void FillMaze(Agent agent)
    {
        Vector3 agentPosition = agent.transform.position;

        Vector2Int cellPos = GetCellFromCoord(agentPosition);

        SetValueIfNotExists(cellPos.x, cellPos.y, 0f);


        var mazeCell = mazeGenerator.GetCellFromVector3(agentPosition);

        MazeValues[cellPos.x][cellPos.y] = Mathf.Min((MazeValues[cellPos.x][cellPos.y] + 1f), 1000000);

        int nDeadEnds = 0;

        foreach (var vCell in mazeCell.VisibleCells)
        {
            int vxCell = cellPos.x + vCell.x;
            int vyCell = cellPos.y + vCell.y;

            SetValueIfNotExists(vxCell, vyCell, 0f);

            var nCell = mazeGenerator.GetCell(new Vector2Int(vxCell, vyCell));
            MazeValues[vxCell][vyCell] = Mathf.Clamp(MazeValues[vxCell][vyCell] + 0.5f, 0f, 1000000);

            if (nCell.VisibleCells.Count > 1)
            {
                if (MazeDeadEnds[vxCell, vyCell])
                {
                    nDeadEnds += 1;
                }
            }
            else if (nCell != mazeGenerator.LastMazeCell && nCell != mazeGenerator.GetCell(new Vector2Int(0,0)))
            {
                MazeDeadEnds[vxCell, vyCell] = true;
                MazeValues[vxCell][vyCell] = 1000000f;
                nDeadEnds += 1;
            }

            SetCellColor(vxCell, vyCell);
        }
        
        if (nDeadEnds >= mazeCell.VisibleCells.Count - 1)
        {
            MazeDeadEnds[cellPos.x, cellPos.y] = true;
            MazeValues[cellPos.x][cellPos.y] = 1000000f;
        }

        MazeFlaggedPath.Add(agent, new bool[mazeGenerator._mazeWidth, mazeGenerator._mazeHeight]);

        foreach (var pos in agent.PositionsAtTimestep)
        {
            MazeFlaggedPath[agent][pos.x, pos.y] = true;
        }

        SetCellColor(cellPos.x, cellPos.y);
    }


    private void SetCellColor(int xCell, int yCell)
    {

        GameObject cubePrimitive;

        Vector2Int cellKey = new Vector2Int(xCell, yCell);

        if (!mazeCells.ContainsKey(cellKey))
        {
            cubePrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubePrimitive.transform.localScale = new Vector3(CellSize, 0.5f, CellSize);
            cubePrimitive.transform.position = new Vector3(xCell * CellSize + CellSize/2f, 1f, yCell * CellSize + CellSize/2f);
            cubePrimitive.AddComponent<MazeCell>();
            mazeCells.Add(cellKey, cubePrimitive);
        }

        cubePrimitive = mazeCells[cellKey];
        if (cubePrimitive != null)
        {
            Renderer renderer = cubePrimitive.GetComponent<Renderer>();
            float mazeValue = MazeValues[xCell][yCell];
            float colorVal = Mathf.Log(mazeValue, 1000);
            renderer.material.color = new Color(0f, 1f - colorVal, 1f- colorVal, 1f);

            cubePrimitive.GetComponent<MazeCell>().CellValue = mazeValue;
        }
    }

    private Vector2Int GetCellFromCoord(Vector3 position)
    {
        int xCell = Mathf.FloorToInt(position.x / CellSize);
        int yCell = Mathf.FloorToInt(position.z / CellSize);

        return new Vector2Int(xCell, yCell);
    }


    private float computeExplorationReward(Agent agent)
    {
        Vector2Int coords = GetCellFromCoord(agent.transform.position);

        int xCell = coords.x;
        int yCell = coords.y;

        var mazeCell = mazeGenerator.GetCellFromVector3(agent.transform.position);

        float cellValue = Mathf.Clamp(MazeValues[xCell][yCell], 1, 1000000);

        float totalReward = ExplorationReward / (cellValue);

        Vector2 agentPos = new Vector2(agent.transform.position.x, agent.transform.position.z);

        float totalDistance = 0.0f;

        foreach (var visCell in mazeCell.VisibleCells)
        {
            int cxCell = xCell + visCell.x;
            int cyCell = yCell + visCell.y;

            totalDistance += ManhattanDistance(mazeGenerator.MazeCoordToVector3(new Vector2Int(cxCell, cyCell)), agentPos);
        }


        foreach (var visCell in mazeCell.VisibleCells)
        {

            int cxCell = xCell + visCell.x;
            int cyCell = yCell + visCell.y;

            SetValueIfNotExists(cxCell, cyCell, 0.5f);

            if (MazeFlaggedPath.ContainsKey(agent))
                if (MazeFlaggedPath[agent][cxCell, cyCell]) continue;

            float ncReward = Mathf.Clamp(MazeValues[cxCell][cyCell], 0.5f, 1000000f);

            float manDistance = ManhattanDistance(mazeGenerator.MazeCoordToVector3(new Vector2Int(cxCell, cyCell)), agentPos);

            float multiplier = 1f - (manDistance / totalDistance) * (manDistance / totalDistance);

            totalReward +=  (multiplier * multiplier + ExplorationReward) / (ncReward * ncReward);
        }


        return totalReward;
    }

    private void SetValueIfNotExists(int xCell, int yCell, float value)
    {
        if (!MazeValues.ContainsKey(xCell))
        {
            MazeValues[xCell] = new Dictionary<int, float>();
        }

        if (!MazeValues[xCell].ContainsKey(yCell))
        {
            MazeValues[xCell][yCell] = value;
        }
    }

    public float ComputeFitness(Agent agent)
    {

        float fitness = 0.0f;

        //float distance = Mathf.Abs(Vector3.Distance(agent.transform.position, Goal.transform.position));
        float manDistance = ManhattanDistance(new Vector2(agent.transform.position.x, agent.transform.position.z), new Vector2(Goal.transform.position.x, Goal.transform.position.z));
        float distanceReward = DistanceReward / (manDistance * manDistance);

        float explorationReward = 0.0f;

        explorationReward += computeExplorationReward(agent);


        fitness += explorationReward;
        

        if (agent.hasReachedGoal)
        {
            fitness = GoalReward + 1000f * distanceReward;
            return fitness;
        }

        if (agent.CanSeeGoal)
        {
            fitness = (GoalReward/2f) + 1000 * distanceReward;
            return fitness;
        }

        //if (agent.isDead)
        //{
        //    fitness /= 2f;
        //}


        return fitness;
    }


    public List<Agent> Selection(List<Agent> oldPopulation)
    {
        float absMinFitness = Mathf.Abs(FitnessValues.Min(x => x.Value));

        float totalFitness = FitnessValues.Sum(x=>x.Value + absMinFitness);

        List<Agent> newAgents = new List<Agent>();

        for (int selectedAgents = 0; selectedAgents < SelectionSize; selectedAgents++)
        {
            //Agent sAgent = proportionalSelection(oldPopulation, totalFitness, absMinFitness);
            Agent sAgent = tournamentSelection(oldPopulation);

            oldPopulation.Remove(sAgent);
            newAgents.Add(sAgent);
        }



        return newAgents;
    }

    private Agent proportionalSelection(List<Agent> oldPopulation, float totalFitness, float absMinFitness = 0.0f)
    {
        float cumulativeFitness = 0.0f;
        float randomFitness = Random.Range(0.0f, totalFitness);

        foreach (var agent in oldPopulation)
        {
            float fitness = FitnessValues[agent];

            cumulativeFitness += fitness + absMinFitness;

            if (cumulativeFitness >= randomFitness)
            {
                return agent;
            }
        }

        return oldPopulation.First();
    }

    private Agent tournamentSelection(List<Agent> oldPopulation)
    {


        if (oldPopulation.Count <= 0)
        {
            Debug.LogError("Population count is 0");
        }

        float maxFitness = float.MinValue;
        Agent mAgent = oldPopulation.First();

        for (int tournamentIndex = 0; tournamentIndex < oldPopulation.Count && tournamentIndex < TournamentSize; tournamentIndex++)
        {
            int randomIndex = Random.Range(0, oldPopulation.Count);
            Agent cAgent = oldPopulation[randomIndex];
            float cFitness = FitnessValues[cAgent];

            if (cFitness > maxFitness)
            {
                maxFitness = cFitness;
                mAgent = cAgent;
            }
        }

        return mAgent;
    }

    public List<Agent> Crossover(List<Agent> selectedPop, int newPopSize)
    {


        List<List<float>> DNAList = new List<List<float>>();

        int maxFails = 100;

        while (DNAList.Count < newPopSize-1 && maxFails > 0)
        {
            int parent1Index = Random.Range(0, selectedPop.Count);
            int parent2Index = Random.Range(0, selectedPop.Count);

            Agent parent1 = selectedPop[parent1Index];
            Agent parent2 = selectedPop[parent2Index];

            //List<float> childrenDNA = unitCrossover(parent1, parent2);
            //List<float> childrenDNA = averageCrossover(parent1, parent2);
            List<float> childrenDNA = singlePointCrossover(parent1, parent2);
            //List<float> childrenDNA = doublePointCrossover(parent1, parent2);

            if (childrenDNA == null)
            {
                maxFails--;
                continue;
            }

            DNAList.Add(childrenDNA);
        }

        List<Agent> newPopulation = new List<Agent>();

        for (int i = 0; i < DNAList.Count; i++)
        {
            var agent = InstantiateAgent(DNAList[i]);
            newPopulation.Add(agent);
        }

        return newPopulation;
    }

    private List<float> doublePointCrossover(Agent parent1, Agent parent2)
    {
        if (parent1 == parent2)
            return parent1.DNA;

        int minDNACount = Mathf.Min(parent1.DNA.Count, parent2.DNA.Count);

        int crossoverPoint1 = Random.Range(1, minDNACount - 1); // Avoid selecting the endpoints
        int crossoverPoint2 = Random.Range(1, minDNACount - 1); // Avoid selecting the endpoints

        if (crossoverPoint1 > crossoverPoint2)
        {
            int temp = crossoverPoint1;
            crossoverPoint1 = crossoverPoint2;
            crossoverPoint2 = temp;
        }

        List<float> child1DNA = new List<float>(parent1.DNA.GetRange(0, crossoverPoint1));
        child1DNA.AddRange(parent2.DNA.GetRange(crossoverPoint1, crossoverPoint2 - crossoverPoint1));
        child1DNA.AddRange(parent1.DNA.GetRange(crossoverPoint2, parent1.DNA.Count - crossoverPoint2));

        return child1DNA;
    }

    private List<float>singlePointCrossover(Agent parent1, Agent parent2)
    {
        if (parent1 == parent2)
            return parent1.DNA;

        int minDNACount = Mathf.Min(parent1.DNA.Count, parent2.DNA.Count);

        int crossoverPoint = Random.Range(1, minDNACount - 1); // Avoid selecting the endpoints


        List<float> child1DNA = new List<float>(parent1.DNA.GetRange(0, crossoverPoint));
        child1DNA.AddRange(parent2.DNA.GetRange(crossoverPoint, parent2.DNA.Count - crossoverPoint));


        return child1DNA;
    }

    private List<float> averageCrossover(Agent parent1, Agent parent2)
    {
        if (parent1 == parent2)
            return parent1.DNA;

        int DNACount = Mathf.Min(parent1.DNA.Count, parent2.DNA.Count);

        List<float> child1DNA = new List<float>();

        float parent1Fitness = FitnessValues[parent1];
        float parent2Fitness = FitnessValues[parent2];
        float totalFitness = parent1Fitness + parent2Fitness;

        for (int i = 0; i < DNACount; i++)
        {
            float weightedAngle = parent1.DNA[i] * (parent1Fitness / totalFitness) + parent2.DNA[i] * (parent2Fitness / totalFitness);
            child1DNA.Add(weightedAngle);
        }

        var maxDna = parent1.DNA.Count > parent2.DNA.Count ? parent1.DNA : parent2.DNA;

        child1DNA.AddRange(maxDna.GetRange(DNACount, maxDna.Count - DNACount));

        return child1DNA;
    }

    private List<float> unitCrossover(Agent parent1, Agent parent2)
    {
        if (parent1 == parent2)
            return parent1.DNA;

        List<float> childDNA = new List<float>();

        int minDNA = Mathf.Min(parent1.DNA.Count, parent2.DNA.Count);

        List<float> maxDna = parent1.DNA.Count > parent2.DNA.Count ? parent1.DNA : parent2.DNA;

        for (int i = 0; i < minDNA; i++)
        {
            int pDNA = Random.Range(0, 2);

            if (pDNA == 0)
                childDNA.Add(parent1.DNA[i]);
            else
                childDNA.Add(parent2.DNA[i]);
        }

        childDNA.AddRange(maxDna.GetRange(minDNA, maxDna.Count - minDNA));

        return childDNA;
    }

    public List<Agent> Mutation(List<Agent> population, float mutationRate)
    {
        Agent maxAgent = FitnessValues.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
        List<float> maxDNA = new List<float>(maxAgent.DNA); 

        foreach (var agent in population)
        {
            for (int d = 1; d < agent.DNA.Count; d++)
            {
                float randomValue = Random.Range(0.0f, 1.0f);
                if (agent.isDead && randomValue <= mutationRate)
                {

                        float newAngle = GaussianDistribution.GenerateRandomGaussian(0, Mathf.PI / 3f);
                        agent.DNA[d] = newAngle;
                    
                }
                //else if (randomValue <= mutationRate)
                //{
                //    float newAngle = GaussianDistribution.GenerateRandomGaussian(0, Mathf.PI);
                //    agent.DNA[d] += newAngle;
                //}                
            }

            agent.isDead = false;
        }

        var mAgent = InstantiateAgent(maxDNA);
        mAgent.isDead = false;
        population.Add(mAgent);

        return population;
    }

    private void ComputeNewGeneration()
    {
        //Disable all agents
        foreach (var a in Population)
        {
            FillMaze(a);
            AgentPooler.Instance.DestroyToPool(a);
        }

 

        // Update Exploration Maze
        //foreach (var coords in CoordsToUpdate)
        //{
        //    FillMaze(coords.Key, coords.Value);
        //}

        List<Agent> oldPopulation = Population;
        List<Agent> newPopulation = new List<Agent>();

        float totalFitness = 0.0f;
        Agent maxAgent = null;
        float maxFitness = 0.0f;
        foreach (Agent agent in oldPopulation)
        {
            float fitness = ComputeFitness(agent);
            totalFitness += fitness;
            FitnessValues[agent] = fitness;

            if (fitness >= maxFitness)
            {
                maxFitness = fitness;
                maxAgent = agent;
            }
        }


        if (maxFitness < SimilarFitnessThreshold)
        {
            nGenSimilarFitness = nGenSimilarFitness + 1;
        }
        else
        {
            nGenSimilarFitness = 0;
        }
       

        Debug.Log("Generation: " + CurrentGen + " Current Population: " + oldPopulation.Count);
        Debug.Log("Average Fitnes: " + totalFitness / oldPopulation.Count + " Max Fitness: " + maxFitness + " Min Fitness: " + FitnessValues.Min(x => x.Value));

        int newPopSize = oldPopulation.Count + Random.Range(-NewGenerationSizeOffset, NewGenerationSizeOffset + 1);
        newPopSize = Mathf.Clamp(newPopSize, MinPopulationSize, MaxPopulationSize);

        newPopulation = Selection(oldPopulation);
        newPopulation = Crossover(newPopulation, newPopSize);
        newPopulation = Mutation(newPopulation, MutationRate + MutationRateIncrement * nGenSimilarFitness);

        //SetDeadEnds();
        //SetBackTracking();
        FitnessValues.Clear();
        MazeFlaggedPath.Clear();

        Population = newPopulation;

        foreach (var agent in Population)
        {
            agent.transform.position = this.transform.position;
            agent.transform.rotation = Quaternion.identity;

            FitnessValues.Add(agent, 0.0f);
        }


    }

    private IEnumerator GenerationsRoutine()
    {
        for (int i = 0; i < PopulationStartSize; i++)
        {
            Agent cGent = InstantiateAgent();
            cGent.name = "Agent_" + i;
            cGent.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);

            Population.Add(cGent);
            FitnessValues.Add(cGent, 0.0f);
        }

        while (CurrentGen < MaxGenerations)
        {
            float currentGenerationTime = 0.0f;
            CompletedAgents = 0;
            while (CompletedAgents < Population.Count && currentGenerationTime < GenerationMaxTime)
            {
                yield return null;
                currentGenerationTime += Time.deltaTime;
            }

            if (CompletedAgents < Population.Count)
            {
                GenerationMaxTime += GenerationTimeIncrement;
                Agent.GenerationTime = GenerationMaxTime;
            }


            ComputeNewGeneration();
            CurrentGen++;
        }

        //Disable all agents
        foreach (var a in Population)
        {
            AgentPooler.Instance.DestroyToPool(a);
        }

    }

    private Agent InstantiateAgent(List<float> DNA = null)
    {
        Agent cGent = AgentPooler.Instance.InstantiateFromPool(this.transform, false);
        cGent.Manager = this;
        if (DNA != null)
            cGent.DNA = DNA;

        cGent.gameObject.SetActive(true);
        return cGent;
    }

    public void OnAgentCompletion(Agent agent)
    {
        CompletedAgents += 1;
    }
}
