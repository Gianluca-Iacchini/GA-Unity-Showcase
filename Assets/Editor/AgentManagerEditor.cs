using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using static AgentManager;
using TMPro;

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
        agentManager.PopulationStartSize = EditorGUILayout.IntField("Start Size", agentManager.PopulationStartSize);
        agentManager.MinPopulationSize = EditorGUILayout.IntField("Min Size", agentManager.MinPopulationSize);
        agentManager.MaxPopulationSize = EditorGUILayout.IntField("Max Size", agentManager.MaxPopulationSize);
        agentManager.NewGenerationSizeOffset = EditorGUILayout.IntField("New Generation Offset", agentManager.NewGenerationSizeOffset);
        agentManager.MaxGenerations = EditorGUILayout.IntField("Max Generations", agentManager.MaxGenerations);

        DrawHeader("Simulation Time Settings");
        agentManager.TimeStep = EditorGUILayout.FloatField("Time Step", agentManager.TimeStep);
        agentManager.GenerationMaxTime = EditorGUILayout.FloatField("Generation Max Time", agentManager.GenerationMaxTime);
        agentManager.GenerationTimeIncrement = EditorGUILayout.FloatField("Generation Time Increment", agentManager.GenerationTimeIncrement);

        DrawHeader("Selection Settings");
        agentManager.SelectionMethod = (AgentManager.SelectionType)EditorGUILayout.EnumPopup("Selection Method", agentManager.SelectionMethod);
        agentManager.SelectionSize = EditorGUILayout.IntField("Selection Size", agentManager.SelectionSize);

        if (agentManager.SelectionMethod == AgentManager.SelectionType.Tournament)
            agentManager.TournamentSize = EditorGUILayout.IntField("Tournament Size", agentManager.TournamentSize);

        agentManager.ElitismSize = EditorGUILayout.IntField("Elitism Size", agentManager.ElitismSize);

        DrawHeader("Crossover Settings");
        agentManager.CrossoverMethod = (CrossoverType)EditorGUILayout.EnumPopup("Crossover Method", agentManager.CrossoverMethod);

        DrawHeader("Mutation Settings");
        agentManager.MutationRate = EditorGUILayout.FloatField("Mutation Rate", agentManager.MutationRate);
        agentManager.MutationRateIncrement = EditorGUILayout.FloatField("Mutation Rate Increment", agentManager.MutationRateIncrement);
        agentManager.SimilarFitnessThreshold = EditorGUILayout.FloatField("Similar Fitness Threshold", agentManager.SimilarFitnessThreshold);

        DrawHeader("Fitness Function Settings");
        agentManager.DeathPenalty = EditorGUILayout.FloatField("Death Penalty", agentManager.DeathPenalty);
        agentManager.DistanceReward = EditorGUILayout.FloatField("Distance Reward", agentManager.DistanceReward);
        agentManager.LifeTimeReward = EditorGUILayout.FloatField("Life Time Reward", agentManager.LifeTimeReward);
        agentManager.ExplorationReward = EditorGUILayout.FloatField("Exploration Reward", agentManager.ExplorationReward);
        agentManager.GoalReward = EditorGUILayout.FloatField("Goal Reward", agentManager.GoalReward);

        DrawHeader("UI Settings");
        agentManager._generationText = EditorGUILayout.ObjectField("Generation Text", agentManager._generationText, typeof(TextMeshProUGUI), true) as TextMeshProUGUI;
        agentManager._fitnessText = EditorGUILayout.ObjectField("Fitness Text", agentManager._fitnessText, typeof(TextMeshProUGUI), true) as TextMeshProUGUI;
    }

    private void DrawHeader(string headerText)
    {
        GUILayout.Space(5f);
        EditorGUILayout.LabelField(headerText, EditorStyles.boldLabel);;
    }
    
}
