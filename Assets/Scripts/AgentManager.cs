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

    // Selection Settings
    [HideInInspector]
    public SelectionType SelectionMethod = SelectionType.Tournament;
    [HideInInspector]
    public int SelectionSize = 10;
    [HideInInspector]
    public int TournamentSize = 5;
    [HideInInspector]
    public int ElitismSize = 5;

    // Crossover Settings
    [HideInInspector]
    public CrossoverType CrossoverMethod = CrossoverType.SinglePoint;

    // Mutation Settings
    [HideInInspector]
    public float MutationRate = 0.01f;
    [HideInInspector]
    public float MutationRateIncrement = 0.02f;
    [HideInInspector]
    public float SimilarFitnessThreshold = 3f;

    // Reward Settings
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

    // UI
    [HideInInspector]
    public TextMeshProUGUI _generationText;
    [HideInInspector]
    public TextMeshProUGUI _fitnessText;    
    [HideInInspector]
    public TextMeshProUGUI _mutationText;

    [System.NonSerialized]
    public float CellSize = 5.0f;

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

    float oldMaxFitness = 0.0f;

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

    private void Update()
    {

        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!isRestartingRoutine)
                StartCoroutine(RestartRoutine());
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
        // Reset agent setting
        AgentData.TimeStep = TimeStep;
        AgentData.GenerationTime = GenerationMaxTime;
        AgentData.Speed = AgentSpeed;
        Agent.Goal = Goal;

        // Generate a random maze
        var maze = mazeGenerator.GenerateMaze();
        var startCell = maze[0, 0];
        var endCell = mazeGenerator.LongestDeadEnd;
        CellSize = mazeGenerator._cellSize;

        // Set the agent's starting and goal positions
        this.transform.position = new Vector3(startCell.transform.position.x, 0.25f, startCell.transform.position.z);
        Goal.transform.position = new Vector3(endCell.transform.position.x, 0.25f, endCell.transform.position.z);

        // Used for finding and storing the maze dead ends and where the agents came from
        MazeDeadEnds = new bool[maze.GetLength(0), maze.GetLength(1)];
        MazeFlaggedPath = new Dictionary<Agent, bool[,]>();

        // Start the simulation
        simulationRoutine = StartCoroutine(GenerationsRoutine());
    }

   
    /// <summary>
    /// Compute the Manhattan distance between two points
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static float ManhattanDistance(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    /// <summary>
    /// Fill the maze value table based on the agent current position and the cells that are visible to the agent
    /// </summary>
    /// <param name="agent"></param>
    private void FillMaze(Agent agent)
    {
        // Get the agent's current position in cell coordinates
        Vector3 agentPosition = agent.transform.position;
        Vector2Int cellPos = GetCellFromCoord(agentPosition);

        // If the cell wasn't visited before, set its value to 0
        SetValueIfNotExists(cellPos.x, cellPos.y, 0f);

        // Get the maze cell that the agent is currently in
        var mazeCell = mazeGenerator.GetCellFromVector3(agentPosition);

        // Decrease the value of the cell the agent is currently in (higher number means that cell is rewarded less)
        MazeValues[cellPos.x][cellPos.y] = Mathf.Min((MazeValues[cellPos.x][cellPos.y] + Population.Count * 0.001f), 1000000);

        int nDeadEnds = 0;

        foreach (var vCell in mazeCell.VisibleCells)
        {
            // Compute the cell coordinates of neighbouring cells that are visible to the agent
            int vxCell = cellPos.x + vCell.x;
            int vyCell = cellPos.y + vCell.y;

            // If the cell wasn't visited before, set its value to 0.5
            SetValueIfNotExists(vxCell, vyCell, 0.5f);

            var nCell = mazeGenerator.GetCell(new Vector2Int(vxCell, vyCell));
            MazeValues[vxCell][vyCell] = Mathf.Clamp(MazeValues[vxCell][vyCell] + Population.Count * 0.005f, 0f, 1000000);

            // Finds if the agent is in a dead end or near one
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
        
        // If all the neighbouring cells are dead ends (except the one the agent came from), then the current cell is a dead end
        if (nDeadEnds >= mazeCell.VisibleCells.Count - 1)
        {
            MazeDeadEnds[cellPos.x, cellPos.y] = true;
            MazeValues[cellPos.x][cellPos.y] = 900000f;
        }

        // Flag the path that the agent took
        MazeFlaggedPath.Add(agent, new bool[mazeGenerator._mazeWidth, mazeGenerator._mazeHeight]);


        foreach (var pos in agent.PositionsAtTimestep)
        {
            MazeFlaggedPath[agent][pos.x, pos.y] = true;
        }

        SetCellColor(cellPos.x, cellPos.y);
    }


    /// <summary>
    /// Changes the color of the cell based on its value (i.e. how many time it has been visited by the agents)
    /// </summary>
    /// <param name="xCell"></param>
    /// <param name="yCell"></param>
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

    /// <summary>
    /// Transforms a position in world coordinates to maze cell coordinates
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    private Vector2Int GetCellFromCoord(Vector3 position)
    {
        int xCell = Mathf.FloorToInt(position.x / CellSize);
        int yCell = Mathf.FloorToInt(position.z / CellSize);

        return new Vector2Int(xCell, yCell);
    }

    /// <summary>
    /// Utility function to set the value of a cell in the maze value table
    /// </summary>
    /// <param name="xCell"></param>
    /// <param name="yCell"></param>
    /// <param name="value"></param>
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

    /// <summary>
    /// Compute the fitness of an agent
    /// </summary>
    /// <param name="agent"></param>
    /// <returns></returns>
    public float ComputeFitness(Agent agent)
    {

        float fitness = 0.0f;
        float explorationReward = 1f;

        float manDistance = ManhattanDistance(new Vector2(agent.transform.position.x, agent.transform.position.z), new Vector2(Goal.transform.position.x, Goal.transform.position.z));
        explorationReward *= computeExplorationReward(agent);

        fitness += explorationReward - DeathPenalty;
        

        if (agent.hasReachedGoal)
        {
            fitness = GoalReward * (1000f / (manDistance));
            return fitness;
        }

        if (agent.CanSeeGoal)
        {
            fitness = (GoalReward / 2f) * (1000f / (manDistance));
            return fitness;
        }


        return fitness;
    }


    /// <summary>
    /// Compute the exploration reward for the agent.
    /// </summary>
    /// <param name="agent"></param>
    /// <returns></returns>
    private float computeExplorationReward(Agent agent)
    {
        // Get the agent's current position in cell coordinates
        Vector2Int coords = GetCellFromCoord(agent.transform.position);
        int xCell = coords.x;
        int yCell = coords.y;

        // Get the maze cell that the agent is currently in
        var mazeCell = mazeGenerator.GetCellFromVector3(agent.transform.position);
        float cellValue = Mathf.Clamp(MazeValues[xCell][yCell], 1, 1000000);

        // Set to 1f to avoid multiplying many small numbers
        float totalReward = 1f;

        // If the agent is not in a dead end, it gets a reward based on the current cell value
        if (!MazeDeadEnds[xCell, yCell])
        {
            totalReward += ExplorationReward / (cellValue);
        }


        // The agent gets a reward based on how many distinct cells it has visited that are not dead ends
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

        // The agent gets a penalty if it visited the same cell more than once
        travelReward = DistanceReward + agent.agentData.DeathIndex * (nValidPos / (nDuplicatePositions + 0.2f));


        Vector2 agentPos = new Vector2(agent.transform.position.x, agent.transform.position.z);

        float closestCellDistance = CellSize * 2f;
        int closestCellX = xCell;
        int closestCellY = yCell;
        float closestCellValue = 10000f;


        // Look at all the neighbouring cells that are visible to the agent and find the one with the lowest value (which menas it has the highest reward)
        // The agent gets a reward based on how close it is to the cell with the lowest value
        foreach (var visCell in mazeCell.VisibleCells)
        {
            int cxCell = xCell + visCell.x;
            int cyCell = yCell + visCell.y;

            SetValueIfNotExists(cxCell, cyCell, 0.5f);

            // Cells that are part of the path that the agent took are ignored
            if (MazeFlaggedPath.ContainsKey(agent))
                if (MazeFlaggedPath[agent][cxCell, cyCell]) { continue; }

            // Cells that are dead ends are also ignored
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

        totalReward *= (ExplorationReward) / (closestCellValue);

        return (totalReward * travelReward) + (1f + (1f/closestCellValue)) * (ExplorationReward / closestCellDistance);
    }

    /// <summary>
    /// Select the agents that will act as parents of the next generation based on the algorithm chosen.
    /// </summary>
    /// <param name="oldPopulation"></param>
    /// <returns></returns>
    public List<AgentData> Selection(List<Agent> oldPopulation)
    {
        float absMinFitness = Mathf.Abs(FitnessValues.Min(x => x.Value));

        float totalFitness = FitnessValues.Sum(x=>x.Value + absMinFitness);

        List<AgentData> newAgentDNA = new List<AgentData>();

        for (int selectedAgents = 0; selectedAgents < SelectionSize; selectedAgents++)
        {
            Agent sAgent;
            switch (SelectionMethod)
            {
                case SelectionType.Proportional:
                    sAgent = proportionalSelection(oldPopulation, totalFitness, absMinFitness);
                    break;
                case SelectionType.Tournament:
                    sAgent = tournamentSelection(oldPopulation);
                    break;
                default:
                    sAgent = tournamentSelection(oldPopulation);
                    break;
            }

            oldPopulation.Remove(sAgent);
            newAgentDNA.Add(sAgent.agentData);
        }



        return newAgentDNA;
    }

    /// <summary>
    /// With proportional selection, agents with higher fitness are more likely to be chosen.
    /// </summary>
    /// <param name="oldPopulation"></param>
    /// <param name="totalFitness"></param>
    /// <param name="absMinFitness"></param>
    /// <returns></returns>
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

    /// <summary>
    /// In a tournament selection the agents are divided into groups of size TournamentSize and the agent with the highest fitness in each group is chosen.
    /// </summary>
    /// <param name="oldPopulation"></param>
    /// <returns></returns>
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

    /// <summary>
    /// The agents of the new generations are created by combining the DNA of the selected agents.
    /// </summary>
    /// <param name="selectedPop"></param>
    /// <param name="newPopSize"></param>
    /// <returns></returns>
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

            AgentData childrenDNA;

            switch (CrossoverMethod)
            {
                case CrossoverType.Unit:
                    childrenDNA = unitCrossover(parent1, parent2);
                    break;
                case CrossoverType.Average:
                    childrenDNA = averageCrossover(parent1, parent2);
                    break;
                case CrossoverType.SinglePoint:
                    childrenDNA = singlePointCrossover(parent1, parent2);
                    break;
                case CrossoverType.DoublePoint:
                    childrenDNA = doublePointCrossover(parent1, parent2);
                    break;
                default:
                    childrenDNA = singlePointCrossover(parent1, parent2);
                    break;
            }

            if (childrenDNA == null)
            {
                maxFails--;
                continue;
            }

            DNAList.Add(childrenDNA);
        }

        return DNAList;
    }

    /// <summary>
    /// Divide the parents DNA in three parts and combine them to create the child's DNA.
    /// </summary>
    /// <param name="parent1"></param>
    /// <param name="parent2"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Divide the parents DNA in two parts and combine them to create the child's DNA.
    /// </summary>
    /// <param name="parent1"></param>
    /// <param name="parent2"></param>
    /// <returns></returns>
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

    /// <summary>
    /// The child DNA is a weighted average of the parents DNA.
    /// </summary>
    /// <param name="parent1"></param>
    /// <param name="parent2"></param>
    /// <returns></returns>
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

    /// <summary>
    /// The child DNA is a random combination of the parents DNA.
    /// </summary>
    /// <param name="parent1"></param>
    /// <param name="parent2"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Mutate the agents DNA based on the mutation rate.
    /// </summary>
    /// <param name="population"></param>
    /// <param name="mutationRate"></param>
    /// <returns></returns>
    public List<AgentData> Mutation(List<AgentData> population, float mutationRate)
    {




        foreach (var agentData in population)
        {

            float randomValue = Random.Range(0.0f, 1.0f);

            // If the child should mutate then always mutate the end where the parent died
            if (randomValue <= mutationRate && agentData.isDead)
            {
                int d = System.Math.Min(agentData.DeathIndex, agentData.DNA.Count) - 2;
                d = Mathf.Max(d, 0);


                for (int i = d; i < agentData.DNA.Count; i++)
                {
                    float newAngle = GaussianDistribution.GenerateRandomGaussian(0, Mathf.PI / 4f);
                    agentData.MutateAtIndex(i, newAngle);
                }

                // Mutate the rest of the DNA with a higher chance of chosing later genes.
                d = GaussianDistribution.RandomlySelectElement(Mathf.Max(d, 0));
                agentData.MutateAtIndex(d, GaussianDistribution.GenerateRandomGaussian(0, Mathf.PI / 4f));
            }      
            
        }
        List<AgentData> maxAgents = FitnessValues.OrderByDescending(x => x.Value).Select(x => x.Key).Take(ElitismSize).ToList();
        population.AddRange(maxAgents);

        return population;
    }

    /// <summary>
    /// Computes the new generation of agents.
    /// </summary>
    private void ComputeNewGeneration()
    {
        
        // Fill the maze value table and disable all the agents.
        foreach (var a in Population)
        {
            FillMaze(a);
            AgentPooler.Instance.DestroyToPool(a);
        }

        List<Agent> oldPopulation = Population;
        List<AgentData> newPopulationDNA = new List<AgentData>();

        float totalFitness = 0.0f;
        Agent maxAgent = null;
        float maxFitness = Mathf.NegativeInfinity;

        // Compute the fitness of each agent; also find the highest fitness and the average fitness of the population.
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
        }

        // Keeps track of how many generations in a row have had similar fitness
        if (maxFitness - oldMaxFitness > SimilarFitnessThreshold)
        {
            nGenSimilarFitness = nGenSimilarFitness + 1;
        }
        else
        {
            nGenSimilarFitness = 0;
        }

        oldMaxFitness = Mathf.Max(maxFitness, oldMaxFitness);
        
        // Update the UI
        _generationText.text = "Generation: " + CurrentGen + "\nPopulation Size: " + oldPopulation.Count;
        _fitnessText.text = "Average: " + totalFitness / oldPopulation.Count + "\nMax: " + maxFitness;
        _mutationText.text = "Mutation Rate: " + (MutationRate + MutationRateIncrement * nGenSimilarFitness);

        // Set the new population size
        int newPopSize = oldPopulation.Count + Random.Range(-NewGenerationSizeOffset, NewGenerationSizeOffset + 1);
        newPopSize = Mathf.Clamp(newPopSize, MinPopulationSize, MaxPopulationSize);

        // Generate the new population DNA
        newPopulationDNA = Selection(oldPopulation);
        newPopulationDNA = Crossover(newPopulationDNA, newPopSize);
        newPopulationDNA = Mutation(newPopulationDNA, MutationRate + MutationRateIncrement * nGenSimilarFitness); //Mutation rate is increased by how often the fitness is similar

        // Clear all the data from the previous generation
        FitnessValues.Clear();
        MazeFlaggedPath.Clear();
        Population.Clear();


        // Instantiate the new agents
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

    /// <summary>
    /// "Main" function of the algorithm. It creates all the new generations and keeps track of the time.
    /// </summary>
    /// <returns></returns>
    private IEnumerator GenerationsRoutine()
    {
        // Instantiate the starting population
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
            // If all the agents have died then we can compute the new generation immediately
            while (CompletedAgents < Population.Count && currentGenerationTime < _generationMaxTime)
            {
                yield return null;
                currentGenerationTime += Time.deltaTime;
            }

            // If not all the agents have died then we increase the simulation time of the next generation
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

    /// <summary>
    /// End the simulation and reset all the data.
    /// </summary>
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

    /// <summary>
    /// Helper function to instatiate the agents
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Callback function for when an agent dies
    /// </summary>
    /// <param name="agent"></param>
    public void OnAgentCompletion(Agent agent)
    {
        CompletedAgents += 1;
    }
}
