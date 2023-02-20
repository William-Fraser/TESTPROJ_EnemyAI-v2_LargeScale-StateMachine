using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.GraphicsBuffer;

public class NavMeshCharacterController : MonoBehaviour
{
    ///keep in mind
    /// accessability to control AI
    /// setting up defaults in start
    /// idle and patrol are the base states, state patterns built off the base are more likely to activate

    ///extras
    /// add idle time for patrol points
    /// CustomEditor to hide unused behaviours

    private enum STATE
    {
        IDLE,
        PATROL,
        ROAM,
        CHASE,
    }

    //variables
    [Header("Traits")] // !!! these are options set in the inspector, they determine what a character can do, // consider moving to a subclass for privacy
    [Tooltip("*Used in tandem with other traits. \nIdle's at Patrol points and during \nChase (if target is lost). ")][SerializeField] private bool idling;
    [Tooltip("*Used in tandem with other traits. \nPatrol is Prioirty over Roam and \noverwrites as the default. ")][SerializeField] private bool patrolling;
    [Tooltip("*Used in tandem with other traits. \nRoam's during Chase instead of Idle \nto 'search' for target before 'losing interest'. ")][SerializeField] private bool roaming;
    [SerializeField] private bool chasing;

    [Header("Patrol")]
    private int patrol_CurrentPoint;
    [SerializeField] private List<GameObject> patrol_Points;

    [Header("Roam")]
    private Vector3 roam_Target;
    [Tooltip("Max roaming Distance")][SerializeField] private int roam_Distance = 5;

    [Header("Chase")]
    private List<GameObject> chase_Targets;
    private Vector3 chase_target;
    [Tooltip("Distance that character will chase targets at")][SerializeField]private float chase_distance = 5;

    [Header("Misc.")]
    private NavMeshAgent agent;
    private STATE returnState;
    [SerializeField] private float movementStopDistance = 1;
    [SerializeField] private STATE state;

    [Header("Timers")] // set up timer randomizer and range capabilities
    private float secondaryStateTimer; // set from IdleTime variants : on time out idle changes to return state
    [SerializeField] private int patrol_IdleTime;
    [SerializeField] private int roam_IdleTime;
    [SerializeField] private int chase_IdleTime;
    [SerializeField] private int chase_RoamTime;

    public void Start()
    {
        //data checks
        if (patrolling && patrol_Points.Count < 2) { Debug.LogError($"{gameObject.name} PATROLERROR: 2 or more patrol points needed"); patrolling = false; return; }
        if (chasing && chase_Targets.Count < 1) { Debug.LogError($"{gameObject.name} CHASEERROR: 1 or more targets needed"); chasing = false; return; }

        //setup starting base state
        if (roaming) { state = STATE.ROAM; }
        if (patrolling) { state = STATE.PATROL; }
        if (!roaming && !patrolling) { state = STATE.IDLE; idling = true; } // if nothing else idling will be true :)

        agent = GetComponent<NavMeshAgent>();
        patrol_CurrentPoint = 0;
    }

    public void Update()
    {

        switch (state)
        { 
            case STATE.IDLE:

                //returns to other state, see SetTimerSecondState();
                if (secondaryStateTimer <= Time.time) ReturnFromSecondState();
                return;

            case STATE.PATROL:

                //init variables
                Vector3 pointPos = patrol_Points[patrol_CurrentPoint].transform.position;
                float distanceFromPoint = Vector3.Distance(pointPos, this.gameObject.transform.position);

                //patrol methods
                MoveTo(pointPos);

                if (distanceFromPoint <= movementStopDistance)
                {
                    ChangePatrolPoint();
                
                    if (idling)
                    {
                        SetState(STATE.IDLE);
                        SetTimerSecondState(patrol_IdleTime, STATE.PATROL);
                    }
                }
                return;

            case STATE.ROAM:

                RoamingMovement();
                return;

            case STATE.CHASE:

                //chase methods
                MoveTo(chase_target);
                TrackChaseTarget();
                return;
        }
    }

    private void MoveTo(Vector3 pointPos)
    {
        //blocker statement
        if (agent.destination == pointPos) return;

        //move to active point
        agent.SetDestination(pointPos);
    }

    #region State Methods

    private void SetState(STATE setState)
    {
        if (setState == state) Debug.LogWarning($"{gameObject.name} WARNING: State was set to the same state");

        state = setState;
    }
    private void SetTimerSecondState(int stateTimer, STATE returnTo) //used in multiple methods, set's a timer for a state with a new state to jump to after a timer
    {
        returnState = returnTo;
        secondaryStateTimer = Time.time + stateTimer;
    }
    private void ReturnFromSecondState()
    {
        if (secondaryStateTimer == 0) return; // block timer from ending every second
        secondaryStateTimer = 0; // stop timer
        SetState(returnState); 
    }
    private STATE GetBaseState()
    { 
        STATE returnState;
        
        returnState = STATE.IDLE;
        if (roaming) returnState = STATE.ROAM;
        if (patrolling) returnState = STATE.PATROL; // patrolling is the top command if available

        return returnState;
    }

    //idle
    public void Idle() // access Idle from controller scripts
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
    public void Patrol() //access patrol from controller scripts
    {
        if (!patrolling) { Debug.LogError($"{gameObject.name} is Patrolling when it is not an active trait"); return; }
        state = STATE.PATROL;
    }

    //roam
    private void RoamingMovement()
    {
        //initiate method objects
        float distanceFromPoint = Vector3.Distance(roam_Target, this.gameObject.transform.position);
        float roamX = Random.Range(0, roam_Distance);
        float roamY = gameObject.transform.position.y;
        float roamZ = Random.Range(0, roam_Distance);
        Vector3 newPos = new Vector3(roamX, roamY, roamZ);
        NavMeshHit closestPos;

        if (distanceFromPoint <= movementStopDistance)
        {
            //calculate random ground position near character and move there
            NavMesh.SamplePosition(newPos, out closestPos, roam_Distance, NavMesh.AllAreas);
            roam_Target = closestPos.position;
            MoveTo(roam_Target);

            //maybe idle for a bit at each position
            if (idling)
            {
                SetTimerSecondState(roam_IdleTime, STATE.ROAM);
                SetState(STATE.IDLE);
            }
        }
    }    
    public void Roam() // access Roam from controller scripts
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

        //calc/start tracking method
        STATE trackingState = STATE.IDLE;
        if (roaming) trackingState = STATE.ROAM;

        switch (trackingState)
        { 
            case STATE.IDLE: SetTimerSecondState(chase_IdleTime, GetBaseState()); SetState(STATE.IDLE); break;
            case STATE.ROAM: SetTimerSecondState(chase_RoamTime, GetBaseState()); SetState(STATE.ROAM); break;
        }
    }
    public void Chase(GameObject gameObject) // used to start chasing from a Source
    {
        //blocker statement
        if (!chasing) { Debug.LogError($"{this.gameObject.name} is Chasing when it is not an active trait"); return; }
        
        Debug.Log($"{this.gameObject.name} is chasing {gameObject.name}");
        
        //setup the chase
        chase_target = gameObject.transform.position;
        state = STATE.CHASE;
    }
    #endregion
    
}
