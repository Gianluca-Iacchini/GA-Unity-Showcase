using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class Agent : MonoBehaviour
{
    
    private float DefaultTargetRadius = 10.0f;

    float fromAngle = 0.0f;
    float toAngle = 0.0f;
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

    public float LifeTime { get; private set; }

    public static GameObject Goal;

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

        Vector3 newPos = this.transform.position;
        newPos += this.transform.forward * DefaultTargetRadius;
        newPos.y = this.transform.position.y;

        _lastPosition = Manager != null ? Manager.transform.position : this.transform.position;
        PositionsAtTimestep.Clear();

        this.hasReachedGoal = false;
        this.CanSeeGoal = false;
        this.Fitness = 0.0f;

        StartCoroutine(SetAngles());
    }

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

    IEnumerator SetAngles()
    {
        int index = 0;
        fromAngle = 0.0f;
        toAngle = 0.0f;
        LifeTime = 0.0f;

        while (this.agentData != null && agentData.DNA.Count == 0)
        {
            yield return null;
        }

        List<float> Angles = agentData.DNA.GetRange(1, agentData.DNA.Count - 1);

        PositionsAtTimestep.Add(GetCurrentCellPosition());



        while (true)
        {
            if (index >= Angles.Count)
            {
                float newDeltaAngle = GaussianDistribution.GenerateRandomGaussian(0, Mathf.PI / 3f);
                Angles.Add(newDeltaAngle);
                agentData.AddGene(newDeltaAngle);
            }
            


            toAngle = fromAngle + Angles[index];
            this.agentData.DeathIndex = index;
            index++;

            t = 0.0f;
            float timeSinceLastStep = 0.0f;


            while (timeSinceLastStep < AgentData.TimeStep)
            {
                TraceRays();
                MoveAgent();

                timeSinceLastStep += Time.deltaTime;
                yield return null;
            }
            PositionsAtTimestep.Add(GetCurrentCellPosition());
            _lastPosition = this.transform.position;
            LifeTime += AgentData.TimeStep;
            fromAngle = toAngle;

        }

    }

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


    private void MoveAgent()
    {
        float radius = DefaultTargetRadius;
        float speed = GetSpeed();
        
        Vector3 targetPoint = MoveTargetPoint(radius);
        this.transform.LookAt(targetPoint);

        if (speed > AgentData.MaxSpeed)
        {
            speed = AgentData.MaxSpeed;
        }

        this.transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

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

    private float GetSpeed()
    {
        float multiplier = agentData.DNA.Count > 0 ? agentData.DNA[0] : AgentData.AverageSpeed;

        Vector3 velocity = Vector3.forward * DefaultTargetRadius;
        velocity.y = 0;


        return velocity.normalized.magnitude * multiplier;
    }


    private Vector3 MoveTargetPoint(float radius)
    {
        float angle = Mathf.Lerp(fromAngle, toAngle, t/AgentData.TimeStep);
        t += Time.deltaTime;

        // Calculate the target's position within the circle around the agent
        float x = this.transform.position.x + radius * Mathf.Cos(angle);
        float z = this.transform.position.z + radius * Mathf.Sin(angle);

        // Update the position of the Target object
        return new Vector3(x, this.transform.position.y, z);
    }
}
