using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AgentData
{
    [NonSerialized]
    public static float TimeStep = 2f;
    public static float GenerationTime = 5f;
    public static float AverageSpeed = 5.0f;
    public static float MaxSpeed = 100.0f;

    public string GUID { get; private set; }

    public List<float> DNA { get; private set; } = new();

    [NonSerialized]
    public bool isDead = false;

    [NonSerialized]
    public int DeathIndex = 0;

    public AgentData()
    {
        ResetDNA();
        if (GUID == null || GUID == "")
        {
            GUID = Guid.NewGuid().ToString();
        }
    }

    public AgentData(List<float> DNA, int deathIndex = 999)
    {
        this.DNA = DNA;
        this.DeathIndex = Mathf.Min(deathIndex, this.DNA.Count);
        this.isDead = false;

        if (GUID == null || GUID == "")
        {
            GUID = Guid.NewGuid().ToString();
        }
    }

    public void ResetDNA()
    {
        System.Random random = new System.Random();
        DNA = Enumerable.Range(0, Mathf.RoundToInt(AgentData.GenerationTime / AgentData.TimeStep)).Select(x => GaussianDistribution.GenerateRandomGaussian(0, Mathf.PI / 3f)).ToList();
        DNA.Insert(0, AgentData.AverageSpeed);
        DNA[0] = Mathf.Clamp(DNA[0], 1f, AgentData.MaxSpeed);
        this.DeathIndex = DNA.Count;

        this.isDead = false;
    }

    public void MutateAtIndex(int d, float newValue)
    {
        if (d < DNA.Count)
        {
            DNA[d] = newValue;
        }
    }

    public void AddGene(float newValue)
    {
        DNA.Add(newValue);
    }

    public void ChangeGUID()
    {
        GUID = Guid.NewGuid().ToString();
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as AgentData);
    }

    public bool Equals(AgentData other)
    {
        return other != null &&
               GUID == other.GUID;
    }

    public override int GetHashCode()
    {
        return GUID.GetHashCode();
    }

    public static bool operator ==(AgentData l, AgentData r)
    {
        if (ReferenceEquals(l, r))
            return true;
        if (ReferenceEquals(l, null) || (ReferenceEquals(r, null)))
            return false;

        return l.Equals(r);
    }

    public static bool operator !=(AgentData l, AgentData r)
    {
        return !(l == r);
    }
}
