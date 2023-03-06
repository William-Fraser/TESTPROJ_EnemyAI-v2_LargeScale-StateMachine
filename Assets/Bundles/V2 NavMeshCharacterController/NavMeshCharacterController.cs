using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;

public class NavMeshCharacterController : MonoBehaviour
{
    ///keep in mind
    /// accessability to control AI
    /// setting up defaults in start
    /// idle and patrol are the base states, state patterns built off the base are more likely to activate

    ///extras
    /// CustomEditor to hide unused behaviours traits

    private enum STATE
    {
        IDLE,
        PATROL,
        ROAM,
        CHASE,
        INTERACT
    }

    //variables
    [Header("Traits")] // !!! these are options set in the inspector, they determine what a character can do, // consider moving to a subclass for privacy
    [Tooltip("*Used in tandem with other traits. \nIdle's at Patrol points and during \nChase (if target is lost). ")][SerializeField] private bool idling;
    [Tooltip("*Used in tandem with other traits. \nPatrol is a Priority State, you can \n use 1 point for a charater to return to a position ")][SerializeField] private bool patrolling;
    [Tooltip("*Used in tandem with other traits. \nRoam's during Chase instead of Idle \nto 'search' for target before 'losing interest'. ")][SerializeField] private bool roaming;
    [SerializeField] private bool chasing;

    [Header("Patrol")]
    private int patrol_CurrentPoint;
    [SerializeField] private List<GameObject> patrol_Points;

    [Header("Roam")]
    private GameObject roam_Target;
    [Tooltip("Max roaming Distance")][SerializeField] private float roam_Distance = 5;

    [Header("Chase")]
    private List<GameObject> chase_Targets;
    private Vector3 chase_target;
    [Tooltip("Distance that character will chase targets at")][SerializeField]private float chase_distance = 5;

    [Header("Interact")]
    private GameObject interact_Target;

    [Header("Misc.")]
    private NavMeshAgent agent;
    public Vector3 moveCheck;
    private Dictionary<int, STATE> timedStates;
    private STATE baseState;
    [SerializeField] private float moveStopDistance = 1;
    [SerializeField] private STATE state;

    [Header("Timers")] // set up timer randomizer and range capabilities
    [SerializeField] private int patrol_IdleTime;
    [SerializeField] private int roam_IdleTime;
    [SerializeField] private int chase_IdleTime;
    [SerializeField] private int chase_RoamTime;


    [Space(20)]
    public float disvisualDEBUG;
    [SerializeField] private STATE[] statevisualDEBUG;
    public int[] timevisualDEBUG;

    public void Start()
    {
        //data checks
        if (patrolling && patrol_Points.Count < 1) { Debug.LogError($"{gameObject.name} PATROLERROR: 1 or more patrol points needed"); patrolling = false; return; }
        if (roam_Distance < moveStopDistance) { Debug.LogError($"{gameObject.name} ROAMERROR: distance is shorter than stopping distance"); return; }
        if (chasing && chase_Targets.Count < 1) { Debug.LogError($"{gameObject.name} CHASEERROR: 1 or more targets needed"); chasing = false; return; }

        //setup starting base state
        if (idling) { state = STATE.IDLE; baseState = STATE.IDLE; }
        if (roaming) { state = STATE.ROAM; baseState = STATE.ROAM; }
        if (patrolling) { state = STATE.PATROL; baseState = STATE.PATROL; }

        agent = GetComponent<NavMeshAgent>();
        patrol_CurrentPoint = 0;
        timedStates = new Dictionary<int, STATE>();
        roam_Target = new GameObject("Roaming State Target");
        roam_Target.transform.parent = this.transform.parent; // set parent to group parent
        SetRoamTargetToClosestNavPos();
    }

    public void Update()
    {
        //Debug.Log(Time.time);
        statevisualDEBUG = timedStates.Values.ToArray();
        timevisualDEBUG = timedStates.Keys.ToArray();
        CheckTimedStates();

        switch (state)
        { 
            case STATE.IDLE: // this is the default see switch doc???

                //idle does nada
                return;

            case STATE.PATROL:

                //init variables
                Vector3 pointPos = patrol_Points[patrol_CurrentPoint].transform.position;
                float distanceFromPoint = Vector3.Distance(pointPos, this.gameObject.transform.position);

                MoveTo(pointPos);
                // change patrol points and maybe idle
                if (distanceFromPoint <= moveStopDistance)
                {
                    ChangePatrolPoint();
                
                    if (idling)
                    {
                        SetState(STATE.IDLE);
                        SetStateTimer(patrol_IdleTime, STATE.PATROL);
                    }
                }
                return;

            case STATE.ROAM: // might lag, then increase idle time with every entity, increasing the time added too maybe :)

                FindRoamingPos();
                MoveTo(roam_Target.transform.position);
                return;

            case STATE.CHASE:

                MoveTo(chase_target);
                TrackChaseTarget();
                return;

            case STATE.INTERACT:

                MoveTo(interact_Target.transform.position);
                InteractWith(interact_Target);
                return;
        }
    }

    private void MoveTo(Vector3 pointPos)
    {
        //blocker statement
        if (moveCheck == pointPos) return;
        agent.SetDestination(pointPos);
        moveCheck = pointPos;
    }

    #region State Methods

    private void SetState(STATE setState)
    {
        if (setState == state) Debug.LogWarning($"{gameObject.name} WARNING: State was set to the same state");

        state = setState;
    }

    private void SetStateTimer(int timeTilTimeOut, STATE switchToOnTimeOut) //used in multiple methods, it's a timer that sets the state after a while, good for lots of things
    {
        int stateTimer = (int)Time.time + timeTilTimeOut;
        timedStates.Add(stateTimer, switchToOnTimeOut);
    }

    private void CheckTimedStates()
    {
        //returns to other state, see SetStateTimer();
        //blocker statements
        if (timedStates.Count == 0) return;
        if (!timedStates.ContainsKey((int)Time.time)) return;

        STATE newState = timedStates[(int)Time.time];

        if (newState == baseState) timedStates.Clear(); // stop timers while at base state, timers can clear for different events like chasing

        SetState(newState); 
    }

    private void SetBaseState(STATE newBaseState)
    {
        if (newBaseState == baseState) Debug.LogWarning($"{gameObject.name} WARNING: Base state was set to the same state");

        baseState = newBaseState;
    }


    //idle
    public void Idle() // access Idle from behaviour scripts
    {
        if (!idling) { Debug.LogError($"{gameObject.name} is Idle when it is not an active trait"); return; }
        state = STATE.IDLE;
    }


    //patrol
    private void ChangePatrolPoint()
    {
        patrol_CurrentPoint++;

        if (patrol_CurrentPoint >= patrol_Points.Count) patrol_CurrentPoint = 0;
    }
    
    public void Patrol() //access patrol from behaviour scripts
    {
        if (!patrolling) { Debug.LogError($"{gameObject.name} is Patrolling when it is not an active trait"); return; }
        state = STATE.PATROL;
    }


    //roam
    private void SetRoamTargetToClosestNavPos()
    {
        //initiate method objects
        float roamX = UnityEngine.Random.Range(-roam_Distance, roam_Distance);
        float roamY = gameObject.transform.position.y;
        float roamZ = UnityEngine.Random.Range(-roam_Distance, roam_Distance);
        Vector3 newPos = new Vector3(roamX, roamY, roamZ);
        NavMeshHit hit;
        
        if (NavMesh.SamplePosition(newPos, out hit, roam_Distance, 1))
            roam_Target.transform.position = hit.position;
        
        Debug.Log($"here1 {hit.position}");
    } // finds a random position / sets it as the roam target

    private void FindRoamingPos()
    {
        float disFromTarget = Vector3.Distance(roam_Target.transform.position, this.gameObject.transform.position);
        Debug.LogWarning("hit2");

        disvisualDEBUG = disFromTarget;

        //find close target if current is outside roam distance
        if (disFromTarget > roam_Distance) 
            SetRoamTargetToClosestNavPos();

        //blocker statement
        if (disFromTarget > moveStopDistance) return;

        //maybe idle for a bit at each position
        if (idling)
        {
            Debug.Log("hit3");
            SetStateTimer(roam_IdleTime, STATE.ROAM);
            SetState(STATE.IDLE);
        }

        //after reaching destination change position
        SetRoamTargetToClosestNavPos();
    }   // checks distance / SetRoamTargetToClosestNavPos / maybe idles
    
    public void Roam() // access Roam from behaviour scripts
    {
        if (!roaming) { Debug.LogError($"{gameObject.name} is Roaming when it is not an active trait"); return; }
        state = STATE.ROAM;
    }


    //chase
    private void TrackChaseTarget()
    {
        //calc distance
        float distanceFromTarget = Vector3.Distance(agent.destination, gameObject.transform.position);

        //blocker statement // target is being chased
        if (distanceFromTarget <= chase_distance) { return; }
        
        Debug.Log($"{gameObject.name} let something get away");

        //calc/start to look for target
        STATE trackingState = STATE.IDLE;
        if (roaming) trackingState = STATE.ROAM;

        switch (trackingState)
        { 
            case STATE.IDLE: SetStateTimer(chase_IdleTime, baseState); SetState(STATE.IDLE); break;
            case STATE.ROAM: SetStateTimer(chase_RoamTime, baseState); SetState(STATE.ROAM); break;
        }
    }

    public void Chase(GameObject gameObject) // used to start chasing from a Source
    {
        //blocker statement
        if (!chasing) { Debug.LogError($"{this.gameObject.name} is Chasing when it is not an active trait"); return; }
        
        Debug.Log($"{this.gameObject.name} is chasing {gameObject.name}");

        //stop state timers to prevent target loss
        timedStates.Clear();

        //setup the chase
        chase_target = gameObject.transform.position;
        state = STATE.CHASE;
    }

    //interact
    private void FindInteractTargetNear(Vector3 Pos)
    { 
        //find interactable object within an area
    }

    public void InteractWith(GameObject gameObject)
    { 
        //if gameobject is a interactable / is within range / interact with it somehow
    }
    #endregion
}
