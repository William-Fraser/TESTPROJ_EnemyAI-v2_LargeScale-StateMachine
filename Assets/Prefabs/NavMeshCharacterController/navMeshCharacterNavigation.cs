using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class navMeshCharacterNavigation : MonoBehaviour
{
    private enum STATE
    {
        IDLE,
        PATROLLING, // to Chasing, from Returning  /                   (set up patrol in square fashion with obstacles between points)
        CHASING,    // to Attacking or Searching, from Patrolling  /    (situational)
        SEARCHING,  // to Chasing or Returning, from Chasing /         (situational)
        ATTACKING,  // to Chasing, from Chasing /                       (used to stop/attack)
        RETURNING,  // to Patrolling from Searching             
        COWERING,
        RETREATING
    }

    //known bugs, chasing state activate but doesn't reset searching timer, works if mesh is chasing on time of coroutine end

    /// public inspector fields

    // navmesh Agent
    [Header("NavMesh Agent")]
    [Space(10)]
    public NavMeshAgent agent;

    //field for Chasing
    [Header("Chase Target")]
    [Space(10)]
    public WhiteList[] whiteList;

    // fields for Attacking
    [Header("Attacking Fields")]
    [Space(10)]
    public float attackSpeed = 2.5f;
    public float attackDamage = 5;

    // fields for Patrol
    [Header("Patrolling Stations")]
    [Space(10)]
    [Range(1 ,4)]
    public int patrolToStation = 1; //set to 1 so the engineer can choose starting Station
    [Space]
    public GameObject station1;
    public GameObject station2;
    public GameObject station3;
    public GameObject station4;

    // fields for StateMaterials
    [Header("State Materials")]
    [Space(10)]
    [Tooltip("Agent Object is used to change state colour of Character")]
    public GameObject agentObject; 
    [Space]
    public Material patrolling;
    public Material chasing;
    public Material searching;
    public Material attacking;
    public Material retreating;

    // private fields
    private GameObject targetObject;
    private STATE _state = STATE.PATROLLING;// enum for states / part of statemachine
    private Material stateDisplaying; // state of display / part of statemachine 
    // fields for Searching
    private Vector3 lastSeen; 
    private Vector3 lastBeen;
    private bool startSearchOnce = true;
    // fields for attacking
    private bool attackReadyTo;
    
    [HideInInspector]
    public GameObject TargetObject { set { targetObject = value; } }

    void Start()
    {
        //init colour of character
        agentObject.GetComponent<MeshRenderer>().material = stateDisplaying;

        targetObject = GetComponentInChildren<GameObject>();

        //init searching values
        lastSeen = this.transform.position;
        lastBeen = this.transform.position;
    }
    void Update()
    {
        Debug.Log("current state: "+_state); //check state                                      // DEBUG
        agentObject.GetComponent<MeshRenderer>().material = stateDisplaying; //set colour
        
        // state Machine
        switch (_state)
        {
            case STATE.PATROLLING:
                // moves character to one Station then sets the destination 
                // for the next Station Number
                /// Station 4 sets destination to 1
                stateDisplaying = patrolling;
                if (patrolToStation == 1)
                {
                    agent.SetDestination(station1.transform.position);
                    if (Vector3.Distance(station1.transform.position, agent.transform.position) < 5)
                    {
                        patrolToStation = 2;
                    }
                }
                else if (patrolToStation == 2)
                {
                    agent.SetDestination(station2.transform.position);
                    if (Vector3.Distance(station2.transform.position, agent.transform.position) < 5)
                    {
                        patrolToStation = 3;
                    }
                }
                else if (patrolToStation == 3)
                {
                    agent.SetDestination(station3.transform.position);
                    if (Vector3.Distance(station3.transform.position, agent.transform.position) < 5)
                    {
                        patrolToStation = 4;
                    }
                }
                else if (patrolToStation == 4)
                {
                    agent.SetDestination(station4.transform.position);
                    if (Vector3.Distance(station4.transform.position, agent.transform.position) < 5)
                    {
                        patrolToStation = 1;
                    }
                }
                break;

            case STATE.CHASING:                                
                // targets chase object within the radius and follows them 
                // until they get close enough to attack or lose object from radius
                stateDisplaying = chasing;
                agent.SetDestination(targetObject.transform.position);
                break;

            case STATE.SEARCHING:
                // moves character to last place chase object was seen and waits
                stateDisplaying = searching;
                agent.SetDestination(lastSeen);
                StartCoroutine(SearchTime());
                if (Vector3.Distance(lastSeen, agent.transform.position) < 5)
                {
                    if (startSearchOnce)
                    {
                        startSearchOnce = false;
                    }
                }
                break;
                
            case STATE.ATTACKING:
                // not really handled yet since it can be coded to do multiple actions / stops from NavMesh
                // the most common one would be to remove health from the player
                /// if Character attacks chase object and removes health, Character should only attack chase object once
                if (attackReadyTo)
                {
                    attackReadyTo = false;
                    StartCoroutine(GetReadyToAttack());
                    Attack();
                }
                stateDisplaying = attacking;
                break;

            case STATE.RETURNING:
                // after Searching wait ends and Character returns to patrol path 
                // at the last place it was patrolling
                stateDisplaying = retreating;
                agent.SetDestination(lastBeen);
                if (Vector3.Distance(lastBeen, agent.transform.position) < 5)
                {
                    _state = STATE.PATROLLING;
                }
                break;
        }
    }

    // set state to Chasing
    private void OnTriggerEnter(Collider other)
    {
        for (int i = 0; i < whiteList.Length; i++)
        { 
            if (whiteList[i].Tag.Contains(other.tag))
            { 
                targetObject = other.gameObject;
                _state = STATE.CHASING;
            }
        }

        if (_state == STATE.PATROLLING) // then save potsition for return state
        {
            lastBeen = this.transform.position;
        }
    }

    // set state to Searching
    private void OnTriggerExit(Collider other)
    {
        if (other.tag == targetObject.tag) // 
        {
            lastSeen = targetObject.transform.position;
            _state = STATE.SEARCHING;
        }
    }

    // set state to Attacking
    private void OnTriggerStay(Collider other)
    {
        if (other.tag == targetObject.tag)
        {
            if (Vector3.Distance(targetObject.transform.position, agent.transform.position) < 3)
            {
                _state = STATE.ATTACKING;
            }
            else 
            {
                _state = STATE.CHASING;
            }
        }
    }

    private void Attack()
    {
        // reduce targets HP>? for now call just call aggrovate
        Aggrovate();
    }

    private void Aggrovate()
    {
        if (targetObject.GetComponent<navMeshCharacterNavigation>())
        {
            targetObject.GetComponent<navMeshCharacterNavigation>().TargetObject = this.gameObject;
        }
    }

    IEnumerator GetReadyToAttack()
    {
        yield return new WaitForSeconds(attackSpeed);
        attackReadyTo = true;
    }

    // set state to Return / time Character waits before Retreating
    IEnumerator SearchTime()
    {
        yield return new WaitForSeconds(10);
        if (_state == STATE.CHASING) yield break;
        _state = STATE.RETURNING;
        startSearchOnce = true;
    }
}
[System.Serializable]
public class WhiteList
{
    public string Tag;
}

