using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityStandardAssets.Characters.ThirdPerson;
using UnityEngine.UI;

public class navMeshCharacterNavigation : MonoBehaviour
{
    private enum STATE
    {
        IDLE,
        PATROL,     // to Chasing, from Returning  /                    (Patrols between set points) needs polish ;p
        CHASE,      // to Attacking or Searching, from Patrolling  /    (attacks player)
        SEARCH,     // to Chasing or Returning, from Chasing /          (searches for player in relative to last pos)
        ATTACK,     // to Chasing, from Chasing /                       (used to stop/attack)
        RETURNING,  // to Patrolling from Searching                     (returns to patrol)
        COWER,      // to Retreat from ANY /                            (used to run away at low health)
        RETREAT,    // to Idle or Spawning from Cower                   (retreats to home base or nearest health pool, if spawn timer is up will always retreat home)
        SPAWN,      // to Returning from Cower /                        (used to multiply enemy after either A[survived low health; below 25%] or B[Enough Time has passed and enemy feels the need to duplicate] option should be added to choose if effects take place)
        AMOUNTOFSTATES // not an actual state quantifies 
    }

    //known bugs, chasing state activate but doesn't reset searching timer, works if mesh is chasing on time of coroutine end

    /// public inspector fields

    // navmesh Agent
    [Header("NavMesh Agent")]
    [Space(10)]
    public GameObject agentObject;

    //field for Chasing
    [Header("Chase Target")]
    [Space(10)]
    public GameObject targetObject;
    public float viewDistance;
    public WhiteList[] whiteList;

    // fields for Attacking
    [Header("Attacking Stats")]
    [Space(10)]
    public float attackSpeed = 2.5f;
    public float attackDamage = 5;

    [Header("Patrolling Stations")]
    [Space(10)]
    [Range(1, 4)]
    public int patrolToStation = 1; //set to 1 so the engineer can choose starting Station
    [Space]
    public GameObject station1;
    public GameObject station2;
    public GameObject station3;
    public GameObject station4;

    [Header("Search Stats")]
    public int searchingTime = 7;

    // fields for StateMaterials
    [Header("CharacterObject")]
    [Space(10)]
    [Tooltip("used to change state colour of Character and raycast from, this object follows the rotation of the NavAgent")]
    public GameObject Character;
    [Range(1, 10f)]
    public float rotationSpeed = 1f;
    [Space]
    public Colours colours;

    [Header("Sound")]
    //sound detection
    public detectSound soundDetect;

    [Header("UI Display")]
    public Text displayStateText;

    // private fields
    private STATE _state = STATE.PATROL;
    private NavMeshAgent agent;
    private RaycastHit hit;
    private Ray ray;
    // Rendering
    private Renderer charRenderer;
    // Chasing
    private int targetPositionMemoryTime = 2; // amount of time until the target is lost
    private bool canRemember = false;
    private bool seesTarget = false;
    // Searching
    private Vector3 lastSeen; 
    private Vector3 lastBeen;
    private bool startSearchOnce = true;
    // attacking
    private bool attackReadyTo = true;

    public ActiveStates activeStates = new ActiveStates();

    [HideInInspector]
    public GameObject TargetObject { set { targetObject = value; } }

    void Start()
    {
        //init agent
        agent = agentObject.GetComponent<NavMeshAgent>();

        //raycasting
        ray.origin = agentObject.transform.position;
        ray.direction = agentObject.transform.TransformDirection(Vector3.back);

        //char rotation
        rotationSpeed /= 100;

        //init colour of character
        charRenderer = Character.GetComponent<Renderer>();
        //charMat.SetColor("_Color", stateDisplaying.color);

        //init searching values
        targetObject = GetComponentInChildren<GameObject>();
        lastSeen = this.transform.position;
        lastBeen = this.transform.position;

        for (int i = 0; i < (int)STATE.AMOUNTOFSTATES; i++)
        {
            activeStates.patrol = false;
            activeStates.chase = false;
            activeStates.attack = false;
            activeStates.search = false;
            activeStates.returning = false;
        }

        activeStates.patrol = true;
    }
    void Update()
    {
        SoundDetection();

        //State showing debug
        if (displayStateText != null)
        {
            switch (_state)
            {
                case STATE.PATROL:
                    charRenderer.material.color = colours.patrol;
                    displayStateText.color = colours.patrol;
                    break;
                case STATE.CHASE:
                    charRenderer.material.color = colours.chase;
                    displayStateText.color = colours.chase;
                    break;
                case STATE.ATTACK:
                    charRenderer.material.color = colours.attack;
                    displayStateText.color = colours.attack;
                    break;
                case STATE.SEARCH:
                    charRenderer.material.color = colours.search;
                    displayStateText.color = colours.search;
                    break;
                case STATE.RETURNING:
                    charRenderer.material.color = colours.returning;
                    displayStateText.color = colours.returning;
                    break;
            }
            displayStateText.text = $"Current State: {_state}";
        }

        // state Machine
        switch (_state)
        {
            case STATE.PATROL:
                Patrol();
                CheckAndSetStateToChase();
                CheckAndSetStateToSearch();
                break;

            case STATE.CHASE:
                Chase();
                CheckAndSetStateToAttack();
                CheckAndSetStateToSearch();
                break;

            case STATE.SEARCH:
                Search();
                CheckAndSetStateToChase();
                CheckAndSetStateToAttack();
                CheckAndSetStateToReturn();
                break;
                
            case STATE.ATTACK:
                Attack();
                CheckAndSetStateToChase();
                CheckAndSetStateToReturn();
                break;

            case STATE.RETURNING:
                Return();
                CheckAndSetStateToChase();
                CheckAndSetStateToPatrol();
                break;
        }
    }

    private void FixedUpdate()
    {
        CastView();
    }

    // set state to Attacking              // probably still the way to do it for v2s
    private void OnTriggerStay(Collider other)
    {
        if (other.tag == targetObject.tag)
        {
            if (Vector3.Distance(targetObject.transform.position, agent.transform.position) < 3)
            {
                _state = STATE.ATTACK;
            }
        }
    }

    /// Private Methods
    #region State Machine Methods
    private void CheckAndSetStateToPatrol()
    {
        if (activeStates.patrol)
        {
            activeStates.SetAllFalse();
            _state = STATE.PATROL;
        }
    }
    private void Patrol()
    {
        // moves character to one Station then sets the destination 
        // for the next Station Number
        /// Station 4 sets destination to 1
        

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
    }
    private void CheckAndSetStateToChase()
    {
        if (activeStates.chase)
        {
            Debug.Log("target found");
            seesTarget = true;
            canRemember = true;

            if (_state == STATE.PATROL) // then save potsition for return state
            {
                lastBeen = this.transform.position;
            }

            targetObject = hit.collider.gameObject;

            activeStates.SetAllFalse();
            _state = STATE.CHASE;

        }
    }
    private void Chase()
    {
        // targets chase object within the radius and follows them 
        // until they get close enough to attack or lose object from radius
        FaceTarget(agent.destination);
        
        agent.SetDestination(targetObject.transform.position);

        if (!canRemember)
        {
            seesTarget = false;
            canRemember = true;
            Debug.LogWarning("target lost" + seesTarget);
            StartCoroutine(ChaseMemory());
        }
    }
    private void CheckAndSetStateToSearch()
    {
        if (activeStates.search)
        {
            activeStates.SetAllFalse();
            _state = STATE.SEARCH;
        }
    }
    private void Search()
    {
        // moves character to last place chase object was seen and waits
        
        agent.SetDestination(lastSeen);

        if (startSearchOnce)
        {
            startSearchOnce = false;
            StartCoroutine(SearchTime());
        }
    }
    private void CheckAndSetStateToAttack()
    {
        if (activeStates.attack)
        {
            activeStates.SetAllFalse();
            _state = STATE.ATTACK;
        }
    }
    private void Attack()
    {
        if (attackReadyTo)
        {
            attackReadyTo = false;
            StartCoroutine(GetReadyToAttack());
            Aggrovate();

            if (targetObject.GetComponent<ThirdPersonUserControl>())
            {
                if (targetObject.GetComponent<ThirdPersonUserControl>().respawning == false)
                {
                    targetObject.GetComponent<ThirdPersonUserControl>().respawning = true; // stealth death 
                                                                                           //StopCoroutine("ChaseMemory");
                    seesTarget = false;
                    agent.SetDestination(transform.position);
                    Debug.Log("kill player");
                    seesTarget = true;

                    activeStates.returning = true;
                }
            }
        }

    }
    private void CheckAndSetStateToReturn()
    {
        if (activeStates.returning)
        {
            activeStates.SetAllFalse();
            _state = STATE.RETURNING;
        }
    }
    private void Return()
    {
        // after Searching wait ends and Character returns to patrol path 
        // at the last place it was patrolling
        
        agent.SetDestination(lastBeen);
        Debug.Log("returning");

        if (Vector3.Distance(lastBeen, agent.transform.position) < 5)
        {
            _state = STATE.PATROL;
        }
    }
#endregion

    private void Aggrovate()
    {
        if (targetObject.GetComponent<navMeshCharacterNavigation>())
        {
            targetObject.GetComponent<navMeshCharacterNavigation>().TargetObject = this.gameObject;
        }
    } 

    private void FaceTarget(Vector3 target)
    {
        Vector3 lookPos = target - agentObject.transform.position;  // set difference
        lookPos.y = 0;                                              // remove Y
        Quaternion rotation = Quaternion.LookRotation(lookPos);     // set rotation as difference
        agentObject.transform.rotation = Quaternion.Slerp(transform.rotation, rotation, rotationSpeed); // Rotate the difference
        //Debug.Log($"rotationSpeed: {rotationSpeed}");
    }

    private void CastView() // v2 set to Chasing
    {
        ray.origin = agentObject.transform.position + (Vector3.up*1.5f);
        ray.direction = agentObject.transform.forward;

        float RayReduction = 6; // views off to the side should be shorter than that in the front
        float castFrequency = .2f; // how often a ray is cast
        float frequencyMultiplier = .1f;
        for (float angle = 1; angle <= 12; angle +=castFrequency) // start at 1 to avoid dividing by 0 ;p
        {
            if (RayReduction >= 1.3f) RayReduction -= castFrequency+castFrequency/castFrequency; // ray reduction rate
            
            castFrequency += frequencyMultiplier;
            frequencyMultiplier += .1f;
            Debug.DrawRay(ray.origin, ray.direction * viewDistance, Color.yellow, .05f, true);             // DEBUG
            Debug.DrawRay(ray.origin, (ray.direction + (agentObject.transform.right / angle)) * (viewDistance-RayReduction), Color.yellow, .05f, true);             // DEBUG
            Debug.DrawRay(ray.origin, (ray.direction + ((agentObject.transform.right * -1) / angle)) * (viewDistance -RayReduction), Color.yellow, .05f, true);
            
            if (
            Physics.Raycast(ray, out hit, viewDistance) || // front cast
            Physics.Raycast(ray.origin, ray.direction + (agentObject.transform.right / angle), out hit, viewDistance) || // right cast
            Physics.Raycast(ray.origin, ray.direction + ((agentObject.transform.right * -1) / angle), out hit, viewDistance))// left cast 
            {
                canRemember = false;
                CheckRayCollison();
            }
        }
        
    }
    private void CheckRayCollison()
    {
        for (int i = 0; i < whiteList.Length; i++)
        {
            if (whiteList[i].Tag.Contains(hit.collider.tag))
            {
                activeStates.chase = true;
            }
        }
    }
    private void SoundDetection()
    {
        if (_state == STATE.CHASE || _state == STATE.ATTACK) return;
        if (soundDetect.objectDetected)
        {
            for (int i = 0; i < whiteList.Length; i++)
            {
                if (whiteList[i].Tag.Contains(soundDetect.detectedObject.tag))
                {
                    // special case of detection without sight
                    if (_state == STATE.PATROL) // then save potsition for return state
                    {
                        lastBeen = this.transform.position;
                    }
                    if (soundDetect.ranges.autoDetect.triggered)
                    {
                        targetObject = soundDetect.detectedObject;
                        activeStates.attack = true;
                    }
                    else
                    {
                        lastSeen = soundDetect.detectedObject.transform.position;
                        activeStates.search = true;
                    }
                }
            }
        }
    }

    #region Coroutines
    IEnumerator GetReadyToAttack()
    {
        yield return new WaitForSeconds(attackSpeed);
        attackReadyTo = true;
    }
    // set state to Return / time Character waits before Retreating
    IEnumerator SearchTime()
    {
        yield return new WaitForSeconds(searchingTime);
        if (_state == STATE.CHASE) yield break;
        activeStates.returning = true;
        startSearchOnce = true;
    }
    // set state to Search / time Character can remember chase target to run after where they think they are (chases actual position)
    IEnumerator ChaseMemory()
    {
        Debug.Log("chasing memeory of target");
        yield return new WaitForSeconds(targetPositionMemoryTime);
        canRemember = false;
        if (seesTarget) yield break;
        lastSeen = targetObject.transform.position;
        activeStates.search = true;
    }
    #endregion
}
[System.Serializable]
public class Colours
{
    public Color patrol;
    public Color chase;
    public Color search;
    public Color attack;
    public Color returning;
}
[System.Serializable]
public class WhiteList
{
    public string Tag;
}
public class ActiveStates
{
    public bool patrol;
    public bool chase;
    public bool search;
    public bool attack;
    public bool returning;

    public void SetAllFalse()
    {
        patrol = false;
        chase = false;
        search = false;
        attack = false;
        returning = false;
    }
}
#region deprecated code
/// set state to Chasing / deprecated
/*private void OnTriggerEnter(Collider other)
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
}*/

/// set state to Searching (dependant on Chase Trigger) / deprecated
/*private void OnTriggerExit(Collider other)
{
    if (other.tag == targetObject.tag) // 
    {

        _state = STATE.SEARCH;
    }
}*/

#endregion