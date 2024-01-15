using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using static AgentManager;
using TMPro;

/// <summary>
/// Class used to customize the AgentManager inspector.
/// </summary>
[CustomEditor(typeof(AgentManager))]
public class AgentManagerEditor : Editor
{
    SerializedProperty selectionMethod;

    SerializedProperty tournamentSize;

    private void OnEnable()
    {
        selectionMethod = serializedObject.FindProperty("selectionMethod");
        tournamentSize = serializedObject.FindProperty("TournamentSize");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        serializedObject.Update();

        var agentManager = target as AgentManager;

        DrawHeader("Agent Settings");
        agentManager.AgentSpeed = EditorGUILayout.FloatField("Agent speed", agentManager.AgentSpeed);

        DrawHeader("Population Settings");
        agentManager.PopulationStartSize = EditorGUILayout.IntField(new GUIContent("Start size", "The starting size of the population."), agentManager.PopulationStartSize);
        agentManager.MinPopulationSize = EditorGUILayout.IntField(new GUIContent("Min Size", "Minimum size of the population at each generation."), agentManager.MinPopulationSize);
        agentManager.MaxPopulationSize = EditorGUILayout.IntField(new GUIContent("Max Size", "Maximum size of the population at each generation."), agentManager.MaxPopulationSize);
        agentManager.NewGenerationSizeOffset = EditorGUILayout.IntField(new GUIContent("New Generation Offset", "How much the population should grow at each generation"), agentManager.NewGenerationSizeOffset);
        agentManager.MaxGenerations = EditorGUILayout.IntField(new GUIContent("Max Generations", "The maximum number of generations before the algorithm ends."), agentManager.MaxGenerations);


        DrawHeader("Simulation Time Settings");
        agentManager.TimeStep = EditorGUILayout.FloatField(new GUIContent("Time Step", "Determines how quickly the agents update their movement."), agentManager.TimeStep);
        agentManager.GenerationMaxTime = EditorGUILayout.FloatField(new GUIContent("Generation Max Time", "Maximum number of time in seconds before the current generation ends."), agentManager.GenerationMaxTime);
        agentManager.GenerationTimeIncrement = EditorGUILayout.FloatField(new GUIContent("Generation Time Increment", "How much time to add to the next generation maximum time in seconds."), agentManager.GenerationTimeIncrement);


        DrawHeader("Selection Settings");
        agentManager.SelectionMethod = (AgentManager.SelectionType)EditorGUILayout.EnumPopup(new GUIContent("Selection Method", "The selection algorithm that should be used."), agentManager.SelectionMethod);
        agentManager.SelectionSize = EditorGUILayout.IntField(new GUIContent("Selection Size", "How many agents to choose as the next generation parents."), agentManager.SelectionSize);


        if (agentManager.SelectionMethod == AgentManager.SelectionType.Tournament)
            agentManager.TournamentSize = EditorGUILayout.IntField(new GUIContent("Tournament Size", "How many agents should there be in each tournament."), agentManager.TournamentSize);
        
        agentManager.ElitismSize = EditorGUILayout.IntField(new GUIContent("Elitism Size", "How many of the best agents to retain at each generation."), agentManager.ElitismSize);


        DrawHeader("Crossover Settings");
        agentManager.CrossoverMethod = (CrossoverType)EditorGUILayout.EnumPopup(new GUIContent("Crossover Method", "The crossover algorithm that should be used."), agentManager.CrossoverMethod);


        DrawHeader("Mutation Settings");
        agentManager.MutationRate = EditorGUILayout.FloatField(new GUIContent("Mutation Rate", "The probability that each children has to mutate."), agentManager.MutationRate);
        agentManager.MutationRateIncrement = EditorGUILayout.FloatField(new GUIContent("Mutation Rate Increment", "How much to increase the mutation rate if the simulation stagnates."), agentManager.MutationRateIncrement);
        agentManager.SimilarFitnessThreshold = EditorGUILayout.FloatField(new GUIContent("Similar Fitness Threshold", "Applies the mutation rate increment to the mutation rate if the average fitness between consecutive generations is less than this value."), agentManager.SimilarFitnessThreshold);


        DrawHeader("Fitness Function Settings");
        agentManager.DeathPenalty = EditorGUILayout.FloatField(new GUIContent("Death Penalty", "How much should the agent be penalized for dying."), agentManager.DeathPenalty);
        agentManager.DistanceReward = EditorGUILayout.FloatField(new GUIContent("Distance Reward", "How much should the agent be rewarded for traveling farther from the starting point."), agentManager.DistanceReward);
        agentManager.LifeTimeReward = EditorGUILayout.FloatField(new GUIContent("Life Time Reward", "How much should the agent be rewarded for staying alive longer."), agentManager.LifeTimeReward);
        agentManager.ExplorationReward = EditorGUILayout.FloatField(new GUIContent("Exploration Reward", "How much should the agent be rewarded for exploring new tiles."), agentManager.ExplorationReward);
        agentManager.GoalReward = EditorGUILayout.FloatField(new GUIContent("Goal Reward", "How much should the agent be rewarded for finding and/or reaching the goal tile."), agentManager.GoalReward);


        DrawHeader("UI Settings");
        agentManager._generationText = EditorGUILayout.ObjectField("Generation Text", agentManager._generationText, typeof(TextMeshProUGUI), true) as TextMeshProUGUI;
        agentManager._fitnessText = EditorGUILayout.ObjectField("Fitness Text", agentManager._fitnessText, typeof(TextMeshProUGUI), true) as TextMeshProUGUI;
        agentManager._mutationText = EditorGUILayout.ObjectField("Mutation Text", agentManager._mutationText, typeof(TextMeshProUGUI), true) as TextMeshProUGUI;
    }

    private void DrawHeader(string headerText)
    {
        GUILayout.Space(5f);
        EditorGUILayout.LabelField(headerText, EditorStyles.boldLabel);;
    }
    
}
