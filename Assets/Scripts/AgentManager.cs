using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEditor.UIElements;
using UnityEngine;

public class AgentManager : MonoBehaviour
{

    // Agent Settings
    [HideInInspector]
    public float AgentSpeed = 15.0f;

    // Population settings
    [HideInInspector]
    public int PopulationStartSize = 10;
    [HideInInspector]
    public int MinPopulationSize = 10;
    [HideInInspector]
    public int MaxPopulationSize = 30;
    [HideInInspector]
    public int NewGenerationSizeOffset = 5;
    [HideInInspector]
    public int MaxGenerations = 100;

    // Time Settings
    [HideInInspector]
    public float TimeStep = 0.5f;
    [HideInInspector]
    public float GenerationMaxTime = 15.0f;
    [HideInInspector]
    public float GenerationTimeIncrement = 0.5f;

    [Header("Selection Settings")]
    [HideInInspector]
    public SelectionType SelectionMethod = SelectionType.Tournament;
    [HideInInspector]
    public int SelectionSize = 10;
    [HideInInspector]
    public int TournamentSize = 5;
    [HideInInspector]
    public int ElitismSize = 5;

    [Header("Crossover Settings")]
    [HideInInspector]
    public CrossoverType CrossoverMethod = CrossoverType.SinglePoint;

    [Header("Mutation Settings")]
    [HideInInspector]
    public float MutationRate = 0.01f;
    [HideInInspector]
    public float MutationRateIncrement = 0.02f;
    [HideInInspector]
    public float SimilarFitnessThreshold = 3f;

    [Header("Fitness Function Settings")]
    [HideInInspector]
    public float DeathPenalty = 100.0f;
    [HideInInspector]
    public float DistanceReward = 1.0f;
    [HideInInspector]
    public float LifeTimeReward = 1.0f;
    [HideInInspector]
    public float ExplorationReward = 10.0f;
    [HideInInspector]
    public float GoalReward = 100.0f;

    [System.NonSerialized]
    public float CellSize = 5.0f;

    [Header("UI Settings")]
    [HideInInspector]
    public TextMeshProUGUI _generationText;
    [HideInInspector]
    public TextMeshProUGUI _fitnessText;

    public enum SelectionType
    {
        Proportional,
        Tournament
    }

    public enum CrossoverType
    {
        Unit,
        Average,
        SinglePoint,
        DoublePoint
    }

    private float _generationMaxTime = 1.0f;

    private Coroutine simulationRoutine;

    private List<Agent> Population;
    
    private Dictionary<AgentData, float> FitnessValues;

    private GameObject Goal;

    private int CompletedAgents = 0;
    private int CurrentGen = 0;



    int nGenSimilarFitness = 0;


    Dictionary<int, Dictionary<int, float>> MazeValues;

    Dictionary<Vector2Int, GameObject> mazeCells;

    MazeGenerator mazeGenerator;

    bool[,] MazeDeadEnds;
    Dictionary<Agent, bool[,]> MazeFlaggedPath;

    private bool isRestartingRoutine = false;

    private void Start()
    {

        Goal = GameObject.FindGameObjectWithTag("Goal");
        Population = new List<Agent>();
        FitnessValues = new Dictionary<AgentData, float>();
        MazeValues = new Dictionary<int, Dictionary<int, float>>();
        mazeCells = new Dictionary<Vector2Int, GameObject>();
        MazeFlaggedPath = new();

        mazeGenerator = GameObject.FindObjectOfType<MazeGenerator>();

        _generationMaxTime = GenerationMaxTime;

    }

    private bool DisplayInfo = false;

    private void Update()
    {

        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!isRestartingRoutine)
                StartCoroutine(RestartRoutine());
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            DisplayInfo = !DisplayInfo;
        }
    }

    private IEnumerator RestartRoutine()
    {
        isRestartingRoutine = true;
        EndSimulation();
        yield return new WaitForSeconds(0.2f);
        StartSimulation();
        isRestartingRoutine = false;
    }

    private void StartSimulation()
    {
        AgentData.TimeStep = TimeStep;
        AgentData.GenerationTime = GenerationMaxTime;
        AgentData.AverageSpeed = AgentSpeed;

        Agent.Goal = Goal;

        var maze = mazeGenerator.GenerateMaze();

        var startCell = maze[0, 0];
        var endCell = mazeGenerator.LongestDeadEnd;

        CellSize = mazeGenerator._cellSize;

        this.transform.position = new Vector3(startCell.transform.position.x, 0.25f, startCell.transform.position.z);
        Goal.transform.position = new Vector3(endCell.transform.position.x, 0.25f, endCell.transform.position.z);

        MazeDeadEnds = new bool[maze.GetLength(0), maze.GetLength(1)];
        MazeFlaggedPath = new Dictionary<Agent, bool[,]>();


        simulationRoutine = StartCoroutine(GenerationsRoutine());
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

        MazeValues[cellPos.x][cellPos.y] = Mathf.Min((MazeValues[cellPos.x][cellPos.y] + Population.Count * 0.0025f), 1000000);

        int nDeadEnds = 0;

        foreach (var vCell in mazeCell.VisibleCells)
        {
            int vxCell = cellPos.x + vCell.x;
            int vyCell = cellPos.y + vCell.y;

            SetValueIfNotExists(vxCell, vyCell, 0.5f);

            var nCell = mazeGenerator.GetCell(new Vector2Int(vxCell, vyCell));
            MazeValues[vxCell][vyCell] = Mathf.Clamp(MazeValues[vxCell][vyCell] + Population.Count * 0.00125f, 0f, 1000000);

            if (nCell.VisibleCells.Count > 1)
            {
                if (MazeDeadEnds[vxCell, vyCell])
                {
                    nDeadEnds += 1;
                }
            }
            else if (nCell != mazeGenerator.LongestDeadEnd && nCell != mazeGenerator.GetCell(new Vector2Int(0,0)))
            {
                MazeDeadEnds[vxCell, vyCell] = true;
                MazeValues[vxCell][vyCell] = 900000f;
                nDeadEnds += 1;
            }

            SetCellColor(vxCell, vyCell);
        }
        
        if (nDeadEnds >= mazeCell.VisibleCells.Count - 1)
        {
            MazeDeadEnds[cellPos.x, cellPos.y] = true;
            MazeValues[cellPos.x][cellPos.y] = 900000f;
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
            cubePrimitive.transform.localScale = new Vector3(CellSize, 0.8f, CellSize);
            cubePrimitive.transform.position = new Vector3(xCell * CellSize + CellSize/2f, -0.4f, yCell * CellSize + CellSize/2f);
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

    private bool shouldDisplayInfo = false;

    private float computeExplorationReward(Agent agent)
    {
        Vector2Int coords = GetCellFromCoord(agent.transform.position);

        int xCell = coords.x;
        int yCell = coords.y;

        var mazeCell = mazeGenerator.GetCellFromVector3(agent.transform.position);

        float cellValue = Mathf.Clamp(MazeValues[xCell][yCell], 1, 1000000);

        float totalReward = 1f;

        if (!MazeDeadEnds[xCell, yCell])
        {
            totalReward += ExplorationReward / (cellValue);
        }


        float travelReward = 1.0f;
        var distinctPos = agent.PositionsAtTimestep.Distinct().ToList();
        float nDuplicatePositions = agent.PositionsAtTimestep.GroupBy(_ => _).Where(_ => _.Count() > 1).Sum(_ => _.Count());

        int nValidPos = distinctPos.Count;
        for (int i = 0; i < MazeDeadEnds.GetLength(0); i++)
        {
            for (int j = 0; j < MazeDeadEnds.GetLength(1); j++)
            {
                if (MazeDeadEnds[i, j])
                {
                    nValidPos -= 1;
                }
            }
        }

        travelReward = 1f + agent.agentData.DeathIndex * (nValidPos / (nDuplicatePositions + 0.2f));


        Vector2 agentPos = new Vector2(agent.transform.position.x, agent.transform.position.z);

        float closestCellDistance = CellSize * 2f;
        int closestCellX = xCell;
        int closestCellY = yCell;
        float closestCellValue = 10000f;

        if (DisplayInfo && shouldDisplayInfo)
        {
            Debug.Log("Agent at world position: " + agent.transform.position);
            Debug.Log("Agent at grid position: " + coords);
        }

        foreach (var visCell in mazeCell.VisibleCells)
        {
            int cxCell = xCell + visCell.x;
            int cyCell = yCell + visCell.y;

            SetValueIfNotExists(cxCell, cyCell, 0.5f);

            if (MazeFlaggedPath.ContainsKey(agent))
                if (MazeFlaggedPath[agent][cxCell, cyCell]) { continue; }

            if (MazeDeadEnds[cxCell, cyCell]) { continue; }

            Vector3 mazeCellPos = mazeGenerator.MazeCoordToVector3(new Vector2Int(cxCell, cyCell));
            float currentDistance = Mathf.Abs(ManhattanDistance(new Vector2(mazeCellPos.x, mazeCellPos.z), agentPos));

            float ncReward = Mathf.Clamp(MazeValues[closestCellX][closestCellY], 0.5f, 1000000f);

            if (ncReward < closestCellValue)
            {
                closestCellValue = ncReward;
                closestCellDistance = currentDistance;
                closestCellX = cxCell;
                closestCellY = cyCell;
            }

        }

        if (DisplayInfo && shouldDisplayInfo)
        {
            Debug.Log("Neighbour cell at world position: " + mazeGenerator.MazeCoordToVector3(new Vector2Int(closestCellX, closestCellY)));
            Debug.Log("Neighbour cell at grid position: " + new Vector2Int(closestCellX, closestCellY));
            Debug.Log("Distance to neighbour cell: " + closestCellDistance);
        }



        totalReward *= (ExplorationReward) / (closestCellValue);

        return (totalReward * travelReward) + (1f / closestCellDistance); /** travelReward + 1f;*/
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

    private bool HasValue(int xCell, int yCell)
    {
        if (!MazeValues.ContainsKey(xCell))
        {
            return false;
        }

        if (!MazeValues[xCell].ContainsKey(yCell))
        {
            return false;
        }

        return true;
    }

    public float ComputeFitness(Agent agent)
    {

        float fitness = 0.0f;

        //float distance = Mathf.Abs(Vector3.Distance(agent.transform.position, Goal.transform.position));
        float manDistance = ManhattanDistance(new Vector2(agent.transform.position.x, agent.transform.position.z), new Vector2(Goal.transform.position.x, Goal.transform.position.z));
        float distanceReward = DistanceReward / (manDistance * manDistance);


        float explorationReward = 1f; 

        explorationReward *= computeExplorationReward(agent);


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


        return fitness;
    }


    public List<AgentData> Selection(List<Agent> oldPopulation)
    {
        float absMinFitness = Mathf.Abs(FitnessValues.Min(x => x.Value));

        float totalFitness = FitnessValues.Sum(x=>x.Value + absMinFitness);

        List<AgentData> newAgentDNA = new List<AgentData>();

        for (int selectedAgents = 0; selectedAgents < SelectionSize; selectedAgents++)
        {
            //Agent sAgent = proportionalSelection(oldPopulation, totalFitness, absMinFitness);
            Agent sAgent = tournamentSelection(oldPopulation);

            oldPopulation.Remove(sAgent);
            newAgentDNA.Add(sAgent.agentData);
        }



        return newAgentDNA;
    }

    private Agent proportionalSelection(List<Agent> oldPopulation, float totalFitness, float absMinFitness = 0.0f)
    {
        float cumulativeFitness = 0.0f;
        float randomFitness = Random.Range(0.0f, totalFitness);

        foreach (var agent in oldPopulation)
        {
            float fitness = FitnessValues[agent.agentData];

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
            float cFitness = FitnessValues[cAgent.agentData];

            if (cFitness > maxFitness)
            {
                maxFitness = cFitness;
                mAgent = cAgent;
            }
        }

        return mAgent;
    }

    public List<AgentData> Crossover(List<AgentData> selectedPop, int newPopSize)
    {


        List<AgentData> DNAList = new List<AgentData>();

        int maxFails = 100;

        while (DNAList.Count < newPopSize - ElitismSize && maxFails > 0)
        {
            int parent1Index = Random.Range(0, selectedPop.Count);
            int parent2Index = Random.Range(0, selectedPop.Count);

            AgentData parent1 = selectedPop[parent1Index];
            AgentData parent2 = selectedPop[parent2Index];

            //List<float> childrenDNA = unitCrossover(parent1, parent2);
            //List<float> childrenDNA = averageCrossover(parent1, parent2);
            AgentData childrenDNA = singlePointCrossover(parent1, parent2);
            //List<float> childrenDNA = doublePointCrossover(parent1, parent2);

            if (childrenDNA == null)
            {
                maxFails--;
                continue;
            }

            DNAList.Add(childrenDNA);
        }

        return DNAList;
    }

    private AgentData doublePointCrossover(AgentData parent1, AgentData parent2)
    {
        if (parent1 == parent2)
            return parent1;

        AgentData minParent = FitnessValues[parent1] < FitnessValues[parent2] ? parent1 : parent2;
        AgentData maxParent = FitnessValues[parent1] > FitnessValues[parent2] ? parent1 : parent2;

        int crossoverPoint1 = Random.Range(1, Mathf.Min(minParent.DNA.Count, maxParent.DNA.Count) - 1);
        int crossoverPoint2 = Random.Range(1, Mathf.Min(minParent.DNA.Count, maxParent.DNA.Count) - 1);

        if (crossoverPoint1 > crossoverPoint2)
        {
            int temp = crossoverPoint1;
            crossoverPoint1 = crossoverPoint2;
            crossoverPoint2 = temp;
        }

        List<float> child1DNA = new List<float>(maxParent.DNA.GetRange(0, crossoverPoint1));
        child1DNA.AddRange(minParent.DNA.GetRange(crossoverPoint1, crossoverPoint2 - crossoverPoint1));
        child1DNA.AddRange(maxParent.DNA.GetRange(crossoverPoint2, maxParent.DNA.Count - crossoverPoint2));

        return new AgentData(child1DNA, maxParent.DeathIndex);
    }

    private AgentData singlePointCrossover(AgentData parent1, AgentData parent2)
    {
        if (parent1 == parent2)
            return parent1;




        AgentData minParent = FitnessValues[parent1] < FitnessValues[parent2] ? parent1: parent2;
        AgentData maxParent = FitnessValues[parent1] > FitnessValues[parent2] ? parent1: parent2;


        int crossoverPoint = Random.Range(1, Mathf.Min(minParent.DNA.Count, maxParent.DNA.Count) - 1);

        List<float> child1DNA = new List<float>(minParent.DNA.GetRange(0, crossoverPoint));
        child1DNA.AddRange(maxParent.DNA.GetRange(crossoverPoint, maxParent.DNA.Count - crossoverPoint));
        int deathIndex = maxParent.DeathIndex;

        return new AgentData(child1DNA, deathIndex);
    }

    private AgentData averageCrossover(AgentData parent1, AgentData parent2)
    {
        if (parent1 == parent2)
            return parent1;

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

        

        var longestParent = parent1.DNA.Count > parent2.DNA.Count ? parent1 : parent2;
        var bestParent = parent1Fitness > parent2Fitness ? parent1 : parent2;
        if (longestParent == bestParent)
        {
            child1DNA.AddRange(longestParent.DNA.GetRange(DNACount, longestParent.DNA.Count - DNACount));
        }


        return new AgentData(child1DNA, bestParent.DeathIndex);
    }

    private AgentData unitCrossover(AgentData parent1, AgentData parent2)
    {
        if (parent1 == parent2)
            return parent1;

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

        var longestParent = parent1.DNA.Count > parent2.DNA.Count ? parent1 : parent2;
        var bestParent = FitnessValues[parent1] > FitnessValues[parent2] ? parent1 : parent2;
        if (longestParent == bestParent)
        {
            childDNA.AddRange(longestParent.DNA.GetRange(minDNA, longestParent.DNA.Count - minDNA));
        }

        return new AgentData(childDNA, bestParent.DeathIndex);
    }

    public List<AgentData> Mutation(List<AgentData> population, float mutationRate)
    {
        List<AgentData> maxAgents = FitnessValues.OrderByDescending(x => x.Value).Select(x => x.Key).Take(ElitismSize).ToList();

        population.AddRange(maxAgents);

        foreach (var agentData in population)
        {

            float randomValue = Random.Range(0.0f, 1.0f);
            if (randomValue <= mutationRate && agentData.isDead)
            {
                int d = System.Math.Min(agentData.DeathIndex, agentData.DNA.Count) - 2;
                d = Mathf.Max(d, 1);

                if (DisplayInfo)
                {
                    Debug.LogWarning("Death index: " + agentData.DeathIndex);
                    Debug.LogWarning("DNA count: " + agentData.DNA.Count);
                    Debug.LogWarning("D value: " + d);
                }


                for (int i = d; i < agentData.DNA.Count; i++)
                {
                    float newAngle = GaussianDistribution.GenerateRandomGaussian(0, Mathf.PI / 2f);
                    agentData.MutateAtIndex(i, newAngle);
                }
            }      
            
        }


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
        List<AgentData> newPopulationDNA = new List<AgentData>();

        float totalFitness = 0.0f;
        Agent maxAgent = null;
        float maxFitness = Mathf.NegativeInfinity;
        shouldDisplayInfo = true;
        foreach (Agent agent in oldPopulation)
        {
            float fitness = ComputeFitness(agent);
            totalFitness += fitness;
            FitnessValues[agent.agentData] = fitness;

            if (fitness >= maxFitness)
            {
                maxFitness = fitness;
                maxAgent = agent;
            }
            shouldDisplayInfo = false;
        }


        if (maxFitness < SimilarFitnessThreshold)
        {
            nGenSimilarFitness = nGenSimilarFitness + 1;
        }
        else
        {
            nGenSimilarFitness = 0;
        }
       
        _generationText.text = "Generation: " + CurrentGen + "\nPopulation Size: " + oldPopulation.Count;
        _fitnessText.text = "Average: " + totalFitness / oldPopulation.Count + "\nMax: " + maxFitness;

        int newPopSize = oldPopulation.Count + Random.Range(-NewGenerationSizeOffset, NewGenerationSizeOffset + 1);
        newPopSize = Mathf.Clamp(newPopSize, MinPopulationSize, MaxPopulationSize);

        newPopulationDNA = Selection(oldPopulation);
        newPopulationDNA = Crossover(newPopulationDNA, newPopSize);
        newPopulationDNA = Mutation(newPopulationDNA, MutationRate + MutationRateIncrement * nGenSimilarFitness);

        //SetDeadEnds();
        //SetBackTracking();
        FitnessValues.Clear();
        MazeFlaggedPath.Clear();
        Population.Clear();

        List<string> strData = new List<string>();

        foreach (var agentData in newPopulationDNA)
        {
            if (FitnessValues.ContainsKey(agentData))
                agentData.ChangeGUID();

            FitnessValues.Add(agentData, 0.0f);

            Agent agent = InstantiateAgent(agentData);
            agent.transform.position = this.transform.position;
            agent.transform.rotation = Quaternion.identity;

            Population.Add(agent);

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
            FitnessValues.Add(cGent.agentData, 0.0f);
        }

        while (CurrentGen < MaxGenerations)
        {
            float currentGenerationTime = 0.0f;
            CompletedAgents = 0;
            while (CompletedAgents < Population.Count && currentGenerationTime < _generationMaxTime)
            {
                yield return null;
                currentGenerationTime += Time.deltaTime;
            }

            if (CompletedAgents < Population.Count)
            {
                _generationMaxTime += GenerationTimeIncrement;
                AgentData.GenerationTime = _generationMaxTime;
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

    private void EndSimulation()
    {
        if (simulationRoutine != null)
            StopCoroutine(simulationRoutine);

        foreach (var agent in Population)
        {
            agent.isResetting = true;
            AgentPooler.Instance.DestroyToPool(agent);
        }

        foreach (var primitive in mazeCells.Values)
        {
            Destroy(primitive);
        }

        Population.Clear();
        FitnessValues.Clear();
        MazeValues.Clear();
        mazeCells.Clear();
        MazeFlaggedPath.Clear();

        mazeGenerator.DestroyMaze();

        CurrentGen = 0;
        CompletedAgents = 0;
        _generationMaxTime = GenerationMaxTime;

    }

    private Agent InstantiateAgent(AgentData data = null)
    {
        Agent cGent = AgentPooler.Instance.InstantiateFromPool(this.transform, false);
        cGent.Manager = this;

        if (data == null)
            data = new AgentData();

        cGent.agentData = data;
        cGent.isDead = false;
        cGent.gameObject.SetActive(true);
        
        
        return cGent;
    }

    public void OnAgentCompletion(Agent agent)
    {
        CompletedAgents += 1;
    }
}
