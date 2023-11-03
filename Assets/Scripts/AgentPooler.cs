using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AgentPooler : MonoBehaviour
{
    public Agent AgentPrefab;
    public int StartingPoolSize = 10;

    private List<Agent> AvailableAgents;

    public static AgentPooler Instance { get; private set; }

    // Start is called before the first frame update
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(Instance);

        AvailableAgents = new List<Agent>();

        for (int i = 0; i < StartingPoolSize; i++)
        {
            Agent agent = Instantiate(AgentPrefab, this.transform);
            AvailableAgents.Add(agent);
        }
    }

    public Agent InstantiateFromPool(Transform parent, bool asActive = true)
    {
        Agent agent;
        if (AvailableAgents.Count > 0)
        {
            agent = AvailableAgents.First();
            AvailableAgents.Remove(agent);
        }
        else
            agent = Instantiate(AgentPrefab, parent);

        agent.transform.parent = parent;
        agent.transform.position = parent.position;
        agent.transform.rotation = Quaternion.identity;
        agent.gameObject.SetActive(asActive);
        return agent;
    }

    public void DestroyToPool(Agent agent)
    {
        if (AvailableAgents.Contains(agent)) return;

        AvailableAgents.Add(agent);
        agent.gameObject.SetActive(false);
    }
}
