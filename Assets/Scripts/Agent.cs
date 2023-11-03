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
    [NonSerialized]
    public static float TimeStep = 2f;
    public static float GenerationTime = 5f;
    public static float AverageSpeed = 5.0f;
    public static float MaxSpeed = 100.0f;

    private Rigidbody _rigidbody;

    float fromAngle = 0.0f;
    float toAngle = 0.0f;
    float t = 0.0f;

    [NonSerialized]
    public AgentManager Manager;

    private GameObject _target;

    private Vector3 _lastPosition;

    [NonSerialized]
    public bool isDead = false;
    [NonSerialized]
    public bool hasReachedGoal = false;
    [NonSerialized]
    public bool CanSeeGoal = false;

    public List<float> DNA = new();

    public float LifeTime { get; private set; }

    public static GameObject Goal;

    public List<Vector2Int> PositionsAtTimestep = new List<Vector2Int>();

    public float Fitness = 0.0f;
    public bool FromMax = false;
    public float NeighbourDistanceReward = 0.0f;
    public float CurrentCellReward = 0.0f;
    

    RaycastHit[] hitColliders = new RaycastHit[3];

    // Start is called before the first frame update
    void Start()
    {
        //_rigidbody = this.GetComponent<Rigidbody>();

        if (DNA.Count == 0)
        {
            System.Random random = new System.Random();
            DNA = Enumerable.Range(0, Mathf.RoundToInt(GenerationTime / TimeStep)).Select(x => GaussianDistribution.GenerateRandomGaussian(0, Mathf.PI / 3f)).ToList();
            DNA.Insert(0, AverageSpeed);
            DNA[0] = Mathf.Clamp(DNA[0], 1f, MaxSpeed);
        }


    }

    private void OnEnable()
    {


        if (_target == null)
            _target = new GameObject(this.gameObject.name + "_Target");
        else
            _target.SetActive(true);

        Vector3 newPos = this.transform.position;
        newPos += this.transform.forward * DefaultTargetRadius;
        newPos.y = this.transform.position.y;
        _target.transform.position = newPos;

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
        if (Goal != null)
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

        if (_target != null)
            _target.SetActive(false);
    }

    IEnumerator SetAngles()
    {
        int index = 0;
        fromAngle = 0.0f;
        toAngle = 0.0f;
        LifeTime = 0.0f;

        while (DNA.Count == 0)
        {
            yield return null;
        }

        List<float> Angles = DNA.GetRange(1, DNA.Count - 1);

        PositionsAtTimestep.Add(GetCurrentCellPosition());



        while (true)
        {
            if (index >= Angles.Count)
            {
                float newDeltaAngle = GaussianDistribution.GenerateRandomGaussian(0, Mathf.PI / 3f);
                Angles.Add(newDeltaAngle);
                DNA.Add(newDeltaAngle);
            }
            


            toAngle = fromAngle + Angles[index];
            index++;
            t = 0.0f;
            float timeSinceLastStep = 0.0f;


            while (timeSinceLastStep < TimeStep)
            {
                TraceRays();
                MoveAgent();
                timeSinceLastStep += Time.deltaTime;
                yield return null;
            }
            PositionsAtTimestep.Add(GetCurrentCellPosition());
            _lastPosition = this.transform.position;
            LifeTime += TimeStep;
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

    private void Update()
    {

    }

    //private void FixedUpdate()
    //{
    //    TraceRays();
    //    MoveAgent();
    //    TraceRays();
    //}

    private void MoveAgent()
    {
        float radius = DefaultTargetRadius;
        float speed = GetSpeed();
        MoveTarget(radius);
        this.transform.LookAt(_target.transform);
        KeepDistance(radius);
        if (speed > MaxSpeed)
        {
            speed = MaxSpeed;
        }

        this.transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void TraceRays()
    {
        var speed = GetSpeed();
        speed = Mathf.Max(speed, MaxSpeed);


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
        float multiplier = DNA.Count > 0 ? DNA[0] : AverageSpeed;

        Vector3 velocity = _target.transform.position - this.transform.position;
        velocity.y = 0;


        return velocity.normalized.magnitude * multiplier;
    }


    private void MoveTarget(float radius)
    {
        float angle = Mathf.Lerp(fromAngle, toAngle, t/TimeStep);
        t += Time.deltaTime;

        // Calculate the target's position within the circle around the agent
        float x = this.transform.position.x + radius * Mathf.Cos(angle);
        float z = this.transform.position.z + radius * Mathf.Sin(angle);

        // Update the position of the Target object
        _target.transform.position = new Vector3(x, this.transform.position.y, z);

        // Increment the angle for the next frame (to move the target around the circle)
    }

    private void KeepDistance(float radius)
    {
        Vector3 agentToTarget = _target.transform.position - this.transform.position;
        agentToTarget.y = 0;

        float distance = agentToTarget.magnitude;
        _target.transform.position += agentToTarget.normalized * (radius - distance);
    }
}
