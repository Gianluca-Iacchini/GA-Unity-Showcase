using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class Agent : MonoBehaviour
{
    public static GameObject Goal;

    float t = 0.0f;

    [NonSerialized]
    public AgentManager Manager;

    private Vector3 _lastPosition;

    public bool isDead { get { return _isDead; } set { _isDead = value; if (agentData != null) agentData.isDead = value; } }

    private bool _isDead = false;

    [NonSerialized]
    public bool hasReachedGoal = false;
    [NonSerialized]
    public bool CanSeeGoal = false;

    [NonSerialized]
    public AgentData agentData;



    public List<Vector2Int> PositionsAtTimestep = new List<Vector2Int>();

    public float Fitness = 0.0f;
    public bool FromMax = false;
    public float NeighbourDistanceReward = 0.0f;
    public float CurrentCellReward = 0.0f;
    

    RaycastHit[] hitColliders = new RaycastHit[3];

    public bool isResetting = false;

    private void OnEnable()
    {
        if (isResetting)
        {
            this.agentData.ResetDNA();
            isResetting = false;
            this.isDead = false;
        }

        _lastPosition = Manager != null ? Manager.transform.position : this.transform.position;
        PositionsAtTimestep.Clear();

        this.hasReachedGoal = false;
        this.CanSeeGoal = false;
        this.Fitness = 0.0f;

        StartCoroutine(SetAngles());
    }

    /// <summary>
    /// When the agent is disabled, check if it can see the goal from both the current position or the last position he was in.
    /// </summary>
    private void OnDisable()
    {
        StopAllCoroutines();

        if (Goal != null && !isResetting)
        {
            if (Physics.Raycast(_lastPosition, Goal.transform.position - this.transform.position, out RaycastHit hit, 1000f, LayerMask.GetMask(new string[] { "Goal", "Wall" })))
            {
                if (hit.collider != null && hit.collider.gameObject != null)
                {
                    this.CanSeeGoal = hit.collider.gameObject.layer == LayerMask.NameToLayer("Goal") && !hasReachedGoal;
                }

            }
            if (Physics.Raycast(this.transform.position, Goal.transform.position - this.transform.position, out RaycastHit hit2, 1000f, LayerMask.GetMask(new string[] { "Goal", "Wall" })))
            {
                if (hit2.collider != null && hit2.collider.gameObject != null)
                {
                    this.CanSeeGoal |= (hit2.collider.gameObject.layer == LayerMask.NameToLayer("Goal") && !hasReachedGoal);
                }

            }
        }
    }

    /// <summary>
    /// Move the agent and rotate it according to the DNA.
    /// </summary>
    /// <returns></returns>
    IEnumerator SetAngles()
    {
        int index = 0;
        float fromAngle = 0.0f;
        float toAngle;

        // If the agent has no DNA, wait until it has one. This happens when the agent is created and the DNA is not yet set.
        while (this.agentData != null && agentData.DNA.Count == 0)
        {
            yield return null;
        }

        List<float> Angles = agentData.DNA;

        // Add the first position to the list of positions visited by the agent.
        PositionsAtTimestep.Add(GetCurrentCellPosition());

        while (true)
        {
            // If the agent DNA is not long enough for the next movement we add a new gene to the DNA.
            if (index >= Angles.Count)
            {
                float newDeltaAngle = GaussianDistribution.GenerateRandomGaussian(0, Mathf.PI / 3f);
                Angles.Add(newDeltaAngle);
                agentData.AddGene(newDeltaAngle);
            }
            

            // Compute the angle the agent should rotate to.
            toAngle = fromAngle + Angles[index];
            this.agentData.DeathIndex = index;
            index++;

            t = 0.0f;
            float timeSinceLastStep = 0.0f;

            // As long as the agent is alive, it rotates and moves forward.
            while (timeSinceLastStep < AgentData.TimeStep)
            {
                // Periodically check if the agent runs into a wall or reaches the goal.
                TraceRays();
                // Move the agent forward
                MoveAgent(fromAngle, toAngle);

                timeSinceLastStep += Time.deltaTime;
                yield return null;
            }
            PositionsAtTimestep.Add(GetCurrentCellPosition());
            _lastPosition = this.transform.position;

            fromAngle = toAngle;

        }

    }

    /// <summary>
    /// Helper function to get the current cell position of the agent.
    /// </summary>
    /// <returns></returns>
    private Vector2Int GetCurrentCellPosition()
    {
        Vector3 pos = this.transform.position;

        int xCell = Mathf.FloorToInt(pos.x / Manager.CellSize);
        int zCell = Mathf.FloorToInt(pos.z / Manager.CellSize);

        if (xCell < 0)
            xCell = 0;
        if (zCell < 0)
            zCell = 0;

        return new Vector2Int(xCell, zCell);
    }


    /// <summary>
    /// Move the agent forward and rotate it according to the DNA.
    /// </summary>
    /// <param name="fromAngle"></param>
    /// <param name="toAngle"></param>
    private void MoveAgent(float fromAngle, float toAngle)
    {
        float radius = AgentData.Speed;
        float speed = GetSpeed();
        
        Vector3 targetPoint = MoveTargetPoint(radius, fromAngle, toAngle);
        this.transform.LookAt(targetPoint);

        if (speed > AgentData.MaxSpeed)
        {
            speed = AgentData.MaxSpeed;
        }

        this.transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    /// <summary>
    /// Check if the agent runs into a wall or reaches the goal.
    /// </summary>
    private void TraceRays()
    {

        var speed = GetSpeed();
        speed = Mathf.Max(speed, AgentData.MaxSpeed);


        var nHits = Physics.BoxCastNonAlloc(this.transform.position, this.transform.localScale, transform.forward, hitColliders, transform.rotation, speed * Time.deltaTime, LayerMask.GetMask(new string[] { "Goal", "Wall" }));

        for (int i = 0; i < nHits; i++)
        {
            var col = hitColliders[i].collider;
            if (col.gameObject.layer == LayerMask.NameToLayer("Goal") && !this.hasReachedGoal)
            {
                hasReachedGoal = true;
                Manager.OnAgentCompletion(this);
                AgentPooler.Instance.DestroyToPool(this);
            }
            else if (col.gameObject.layer == LayerMask.NameToLayer("Wall") && !this.isDead)
            {
                this.isDead = true;

                Manager.OnAgentCompletion(this);
                AgentPooler.Instance.DestroyToPool(this);
            }
            
        }
    }

    /// <summary>
    /// Move the agent forward based on the agent speed.
    /// </summary>
    /// <returns></returns>
    private float GetSpeed()
    {
        float multiplier =  AgentData.Speed;

        Vector3 velocity = Vector3.forward * AgentData.Speed;
        velocity.y = 0;


        return velocity.normalized.magnitude * multiplier;
    }

    /// <summary>
    /// Compute a target point for the agent to look at. The agent will rotate and move towards this point.
    /// </summary>
    /// <param name="radius"></param>
    /// <param name="fromAngle"></param>
    /// <param name="toAngle"></param>
    /// <returns></returns>
    private Vector3 MoveTargetPoint(float radius, float fromAngle, float toAngle)
    {
        // The movement is linearly interpolated between the previous angle and the next angle.
        float angle = Mathf.Lerp(fromAngle, toAngle, t/AgentData.TimeStep);
        t += Time.deltaTime;

        // Calculate the target's position within the circle around the agent
        float x = this.transform.position.x + radius * Mathf.Cos(angle);
        float z = this.transform.position.z + radius * Mathf.Sin(angle);

        // Update the position of the Target object
        return new Vector3(x, this.transform.position.y, z);
    }
}
