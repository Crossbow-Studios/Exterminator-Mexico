public class AIController : MonoBehaviour
{
    public NavMeshAgent agent;
    public ThirdPersonCharacter character;
    public Animator animator;
    public Transform spine;

    public float walkingRange;
    public float accuracy;
    public float detectionRange;
    private Quaternion lastRotation;
    public float rotationSpeed;
    public bool skActive = false;
    public bool playerInRange;
    public PlayerController player;

    public Health health;
    public int leftoverAmmo = 90;

    public float xCorrection = 0; 

    void Start()
    {
        if (player == null) { player = PlayerController.FindObjectOfType<PlayerController>(); }

        agent.updateRotation = false;   //rotation is handeled through animation and not with the transform

        StartCoroutine(Radar());
    }

    void Update()
    {
        if (health.isDead)
        {
            agent.enabled = false;
            return;
        }

        if (agent.remainingDistance > 0.1f) // if not at destination yet.
        {
            //Character moving
            character.Move(agent.desiredVelocity, false, false);
        } else
        {
            //Stop Character
            character.Move(Vector3.zero, false, false);
        }
    }

    private void LateUpdate()
    {
        if (skActive && !health.isDead)
        {
            //Dont want to look up or down --> look straight so equalizing y value
            // The following code is not rendered... only done, then use the values to do a smooth rotation
            Vector3 playerPositionToLookAt = player.transform.position;
            playerPositionToLookAt.y = spine.position.y;

            spine.transform.LookAt(playerPositionToLookAt);

            // rotation is set off by 90 degrees on the x axis
            Vector3 rot = spine.eulerAngles;
            rot.x += xCorrection;                               // this needs to be adjusted for each model cause some spines are somehow rotated..
            spine.eulerAngles = rot;
            Quaternion desiredRotation = spine.rotation;
            // do rotation smoothly
            spine.rotation = Quaternion.RotateTowards(lastRotation, desiredRotation, rotationSpeed * Time.deltaTime);

            //if offset smaller than n, fire weapon;
            if (Quaternion.Angle(spine.rotation, desiredRotation) < accuracy)
            {
                try
                {
                    GetComponentInChildren<WeaponController>().Fire(this.gameObject.name);
                }
                catch {}
            }
        }

        lastRotation = spine.rotation;
    }

    public void UpdateDestination()
    {
        Vector3 destination;
        if (GeneratePoint(walkingRange,out destination))
        {
            agent.SetDestination(destination);
        } else
        {
            Debug.LogWarning("30 attemps of finding spawnposition failed...");
        }
    }

    public void HandleUserInput()
    {
        if (Input.GetButtonUp("PickUp"))    //PickUp key pressed ("F")
        {
            //Pick up something... Continue AI when done with Action;
            agent.isStopped = true;              //dont move
            animator.SetTrigger("PickUp");       // call PickUp animation
        }
    }

    public void AnimationEvent_PickUp()
    {
        animator.ResetTrigger("PickUp"); //prevent spaming animation, while still animating which leads to repeating animation numerous times...
        agent.destination = agent.nextPosition; // set destination to next pathposition ==> Nearly the agent's position ==> Keeps staying;
        agent.isStopped = false; // release agent from stopped mode
    }

    bool GeneratePoint(float spawnRadius, out Vector3 result)
    {
        for (int i = 0; i < 30; i++)    //30 attemps
        {
            Vector3 randomPoint = Random.insideUnitCircle * walkingRange;   //create random vector2            
            Vector3 provisionalPosition = transform.position + new Vector3(randomPoint.x,0,randomPoint.y);  //apply vector2 as plane to ai position
            Debug.DrawLine(provisionalPosition, provisionalPosition + Vector3.up,Color.blue,1);

            NavMeshHit hit;

            if (NavMesh.SamplePosition(provisionalPosition, out hit, 1.0f, NavMesh.AllAreas))   //find position on navmesh from the position with radius 1 
            {
                result = hit.position;
                return true;
            }
        }

        //if 30 attemps fail...
        result = Vector3.zero;
        return false;
    }

    IEnumerator Radar()
    {
        while (true) {
            float distance = 0;     //create new float to store distance to player

            try
            {
                distance = Vector3.Distance(player.transform.position, transform.position);     //get distance to player
            } catch { Debug.LogError("No Playerreference set for AI"); break; }

            if (health.isDead || player.health.isDead)  // if ai is dead or player is dead ---> For respawnmethods -> separate these conditions.... so Radar continues
            {
                playerInRange = false;
                skActive = false;
                break;
            }

            if (distance < detectionRange)      //player is in sightingrange
            {
                if (distance > detectionRange/2)     // player is in range but more than half of sightingrane 
                {
                    Vector3 goTo = (player.transform.position - transform.position) / 2;            //walk to the middle bewtween player and itself
                    agent.SetDestination(transform.position + goTo);
                }

                playerInRange = true;           //set bool to true after if because it needs to check if he already was spotted
                skActive = true;
            } else
            {
                agent.SetDestination(player.transform.position);
                
                playerInRange = false;      //walk to last position and the set var to false.. so ai walks to last known position
                skActive = false;
            }

            yield return new WaitForSeconds(.5f);       //repeat every .5 secs
        }
    }

    public void OverrideDestination()
    {
        if (!playerInRange)
        {
            Vector3 goTo = (player.transform.position - transform.position) / 2;
            agent.SetDestination(transform.position + goTo); 
        }
    }

    void DrawDebug()
    {
        Debug.DrawLine(transform.position, agent.destination,Color.red);
    }

    public void DropWeapon()
    {
        GetComponent<HandIKControl>().Clear();

        PickUpItem weapon = GetComponentInChildren<PickUpItem>();

        weapon.Drop(ammoLeft: leftoverAmmo);
        weapon.transform.parent = null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, detectionRange / 2);
    }
}

public class Caller : MonoBehaviour
{
    public PlayerController link;
}

public class cameraArrived : MonoBehaviour
{
    public MenuController reference;

    public void CameraDidArrive()
    {
        reference.StartLoading();
    }
}

public class CarePackage : MonoBehaviour
{
    public LayerMask layer;
    public float distToGround;

    void Start()
    {
        distToGround = GetComponent<Collider>().bounds.extents.y;   //get height  of object
    }

    private void FixedUpdate()
    {
        RaycastHit hit;

        if (Physics.Raycast(transform.position, Vector3.down, out hit, distToGround + .1f, layer))
        {
            BreakShell();
        }
    }

    public void BreakShell()
    {
        Transform child = transform.GetChild(0);
        child.SetParent(null);
        Destroy(this.gameObject);
    }
}

public class CursorController : MonoBehaviour
{
    public bool forceActive = false;

    public Sprite normalCursor;
    public Sprite secondCursor;

    private Image cursorImage;
    private RectTransform rect;

    void Start()
    {
        rect = GetComponent<RectTransform>();
        cursorImage = GetComponent<Image>();

        Cursor.lockState = CursorLockMode.Confined;     //disable cursor and lock it to center
        Cursor.visible = false;
    }

    void Update()
    {
        cursorImage.enabled = GameController.isPaused || forceActive;
        rect.position = Input.mousePosition;

        if (Input.GetMouseButtonDown(0))
        {
            cursorImage.sprite = secondCursor;
        }

        if (Input.GetMouseButtonUp(0))
        {
            cursorImage.sprite = normalCursor;
        }
    }
}

//-EVENT TRIGGERS-
[System.Serializable]
public class DeathTrigger : UnityEvent{}
public class EnemyDeathTrigger : UnityEvent<AIController>{}
public class InteractionTrigger : UnityEvent<PickUpItem, PlayerController>{}
//----------------

public class GameController : MonoBehaviour
{
    public PlayerController player;

    public int currentScore;
    public int multiplier;
    public float rawMultiplier;
    public float multiplierCooldown;
    private int lastReward = 1;

    public static bool isPaused = false;
    public static bool isAlive = true;

    public Transform aliveEnties;
    public Transform allEntities;

    //UI reference;

    public Slider healthBar;
    public Slider multiplierBar;
    public TMP_Text multiplierIndicator;
    public Animation anim;

    public TMP_Text scoreText;
    public TMP_Text endScoreText;
    public TMP_Text ammoText;
    private Color originalColor;

    public GameObject highScoreIndicator;
    public GameObject overlay;
    public GameObject gameEndOverlay;
    public Animation tutorialOverlay;
    public Animation interactionOverlay;

    public bool guidedMode = true;

    void Start()
    {
        player = PlayerController.FindObjectOfType<PlayerController>();

        UpdateScore();
        healthBar.value = healthBar.maxValue;

        Time.timeScale = 1;
        isPaused = false;
        isAlive = true;

        originalColor = ammoText.color;

        guidedMode = PlayerPrefs.GetInt("showHelp") == 0;       // if stored-var is not set or is 0 -> tutorial enabled

        if (guidedMode)
        {
            StartCoroutine(ShowTutorial(6));
        }
    }

    void Update()
    {
        float deltaCooldown = multiplierCooldown * Time.deltaTime;
        UpdateMultiplier(-deltaCooldown);

        if (!anim.isPlaying)
        {
            anim.Play();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void AIGotKilled(int killScore)
    {
        currentScore += killScore * multiplier;     // the current scorre is multiplied by the multiplier
        UpdateMultiplier(killScore);                // themultiplier is not affected by the current multiplier

        UpdateScore();
    }

    public void PlayerHealthUpdated(int health)
    {
        healthBar.value = health;
    }

    public void UpdateScore()
    {
        scoreText.text = currentScore.ToString();
    }

    public void UpdateMultiplier(float amount)
    {
        rawMultiplier += amount;                                              // add specific amount to raw multiplier

        if (rawMultiplier < 0)
        {
            rawMultiplier = 0;
        }

        multiplier = (int)(rawMultiplier / 100) + 1;                               // a stage steps with every 100 points... normal multiplier is 1
        multiplierBar.value = rawMultiplier - ((multiplier - 1) * 100);    // only show progress of one stage, therefore removeing stages * 100 (a stage equals 100)

        multiplierIndicator.text = multiplier.ToString() + "x";
        multiplierIndicator.enabled = (multiplier != 1);

        if (lastReward < multiplier)
        {
            StageReward(multiplier, lastReward);
            lastReward = multiplier;
        }

        if (lastReward > multiplier + 1)
        {
            lastReward--;
        }
    }

    public void Pause()
    {
        Time.timeScale = 0;             //stop all timerelated function: Update(), Animations, etc.
        isPaused = true;
        overlay.SetActive(true);        //overlay menuscreen
    }

    public void Resume()
    {
        overlay.SetActive(false);       //disable menuscreen
        isPaused = false;               
        Time.timeScale = 1;             //continue timescale
    }

    public void Retry()
    {
        int index = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(index);
    }

    public void Quit()
    {
        SceneManager.LoadScene(0);      //load menu scene
    }

    public void GameEnded()
    {
        Time.timeScale = 1;
        isPaused = true;
        isAlive = false;

        int myScore = currentScore;

        bool newHighScore = SaveScore(currentScore);        //returns true if score is higher

        gameEndOverlay.SetActive(true);                     // show endscreen
        endScoreText.text = currentScore.ToString();
        highScoreIndicator.SetActive(newHighScore);
    }

    public bool SaveScore(int score)
    {
        int highscore = PlayerPrefs.GetInt("highscore");

        if (highscore < score)
        {
            //save new score
            PlayerPrefs.SetInt("highscore", score);
            return true;
        }

        //else dont save the score
        return false;
    }

    public IEnumerator ShowTutorial(int t)
    {
        tutorialOverlay.Play("show");
        yield return new WaitForSeconds(t);
        tutorialOverlay.Play("hide");
    }

    public void ShowInteraction(bool show)
    {

        if (!guidedMode) { return; }    //only show tutorial when in guided mode

        if (show)
        {
            interactionOverlay.Play("show");
        }
        else
        {
            interactionOverlay.Play("hide");
        }
    }

    public void UpdateAmmoVisulaization(int ammo)
    {
        ammoText.text = ammo.ToString();

        if (ammo <= 3)
        {
            ammoText.color = Color.red;
        }
        else
        {
            ammoText.color = originalColor;
        }
    }

    public List<GameObject> reward;

    public void StageReward(int currentStage, int lastReward)
    {
        int difference = currentStage - lastReward;

        for (int i = 1; i <= difference; i++)
        {
            int stage = lastReward + i;
            int index = (stage - 2) % reward.Count;

            DropReward(index);
        }
    }

    public void DropReward(int index)
    {
        int randomX = Random.Range(-2, 2);
        int randomY = Random.Range(-2, 2);
        Vector3 randomPoint = new Vector3(randomX, 20, randomY);
        Vector3 dropLocation = player.transform.position + randomPoint;

        try { GameObject drop = Instantiate(reward[index], dropLocation, Quaternion.identity); } catch { }
    }
}

public class HandIKControl : MonoBehaviour
{
    public bool ikActive = false;
    [Space]

    //Setting reference Objects
    public Transform rightHandObject;
    public Transform leftHandObject;
    public Transform lookAtObject;

    protected Animator animator;

    void Start()
    {
        //reference to local animator
        animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (ikActive)
        {
            if (lookAtObject != null)
            {
                animator.SetLookAtWeight(1);
                animator.SetLookAtPosition(lookAtObject.position);

            }

            if (rightHandObject != null)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);

                animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandObject.position);
                animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandObject.rotation);
            }

            if (leftHandObject != null)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);

                animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandObject.position);
                animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandObject.rotation);
            }
        }
        else
        {
            animator.SetLookAtWeight(0);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0);
        }
    }

    public void Clear()
    {
        ikActive = false;
        rightHandObject = null;
        leftHandObject = null;
        lookAtObject = null;
    }
}

public class Health : MonoBehaviour
{
    public DeathTrigger deathTrigger;

    public int maxHealth;
    public int health;
    public int killScore;
    public int despawn = 30;
    private Animator animator;
    public Animation flashAnimation;
    public bool isDead = false;
    public bool isPlayer = false;

    public GameController game;

    void Start()
    {
        deathTrigger = new DeathTrigger();

        animator = GetComponent<Animator>();
        health = maxHealth;

        if (game == null)
        {
            Debug.LogError("!!! No Reference to GameController !!!");
        }
    }

    public void HitOccured(int damage)
    {
        if (isDead) { return; }

        if (damage > 0)
        {
            //remove health
            flashAnimation.Play("flash");
            //GiveAwayLocation();    not needed thus enemy knows location all the time
        }
        else
        {
            flashAnimation.Play("flashGreen");
        }

        health -= damage;

        if (isPlayer)
        {
            game.PlayerHealthUpdated(health);
        }

        if (health <= 0)
        {
            Die();
        }

        if (health > maxHealth)
        {
            health = maxHealth;
        }
    }

    private void Die()
    {
        isDead = true;
        animator.SetBool("DeathTrigger", true);

        transform.SetParent(game.allEntities);

        //Disable all behaviors
        try
        {
            this.GetComponent<Collider>().enabled = false;
            this.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.Discrete;
            this.GetComponent<Rigidbody>().isKinematic = true;
        }catch { }

        if (isPlayer)
        {
            game.GameEnded();
        }
        else
        {
            Destroy(this.gameObject, despawn);     //despawn
            try
            {
                game.AIGotKilled(killScore);
                GetComponent<AIController>().DropWeapon();
            }catch{} 
        }
    }

    void GiveAwayLocation()
    {
        if (!isPlayer)
        {
            try { GetComponent<AIController>().OverrideDestination(); } catch { }

        }
    }
}

public class HitBehavior : MonoBehaviour
{
    public Transform impact;
    public Health health;
    public bool isAI = false;
    private bool hasHealth;

    void Start()
    {
        hasHealth = health != null;
    }

    public void RegisterHit(int damage)
    {
        if (hasHealth)
        {
            health.HitOccured(damage);
        }
    }

    public void RegisterHit(int damage, Vector3 normal, Vector3 point)
    {
        try
        {
            Instantiate(impact, point, Quaternion.LookRotation(normal));
        }catch{}

        if (hasHealth)
        {
            health.HitOccured(damage);
        }
    }
}

public class MenuController : MonoBehaviour
{
    public CanvasGroup mainMenu;
    public CanvasGroup settings;

    public GameObject LoadingVisualization;
    public Animation CameraZoom;
    public Animation EarthRotation;

    public int selectedIndex;

    public TMP_Text highscoreText;

    public List<string> worldAnimationIndex;
    public List<string> cameraAnimationIndex;

    public void Start()
    {
        mainMenu.interactable = true;
        settings.interactable = false;

        LoadingVisualization.SetActive(false);
        Time.timeScale = 1;
        GetHighscore();
    }

    public void Quit()
    {
        mainMenu.interactable = false;
        settings.interactable = false;
        Application.Quit();
    }
    public void Settings()
    {
        mainMenu.interactable = false;
        settings.interactable = true;
        CameraZoom.Play("toSettings");
    }

    public void BackToMenu()
    {
        mainMenu.interactable = true;
        settings.interactable = false;
        CameraZoom.Play("toMainMenu");
    }

    public void ToggleFullscreen()
    {
        Screen.fullScreen = !Screen.fullScreen;
    }

    public void Play(int sceneIndex)
    {
        selectedIndex = sceneIndex;

        mainMenu.interactable = false;
        settings.interactable = false;

        CameraZoom.Play("camera");  //function_startloading is called when animation is done....

        EarthRotation[worldAnimationIndex[sceneIndex - 1]].speed = 0;
        EarthRotation.Play(worldAnimationIndex[sceneIndex - 1]);
    }

    public void StartLoading()
    {
        LoadingVisualization.SetActive(true);
        StartCoroutine(LoadAsynchronously(selectedIndex));
    }

    IEnumerator LoadAsynchronously(int sceneIndex)
    {
        AnimationState state = EarthRotation[worldAnimationIndex[sceneIndex - 1]]; // reference to earth rotation animation

        float length = state.length;                                            // get length of animation

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);     // set reference to the loading process

        operation.allowSceneActivation = false;     // dont change scene when loaded

        int loop = 1;
        while (state.time != 0 || loop != 0 || operation.progress < .9f)
        {
            float currentProgress = Mathf.Clamp01(operation.progress / .9f);    //progress goes from 0 to 0.9f. Clamp makes it go from 0 to 1

            float time = currentProgress * length;                              //percentage of loadingprocess should be equal the percentage of the animation played

            if (state.time < time)                                              //if played percentage is lower than loading percentage, play video until it is equal
            {
                state.speed = 1;
                loop = 0;
            }
            else
            {
                state.speed = 0;
            }

            yield return null;
        }
        CameraZoom.Play(cameraAnimationIndex[sceneIndex - 1]);                  //zoom into globe
        yield return new WaitForSeconds(1);                                    
        operation.allowSceneActivation = true;                                  //allow tho change scene when loaded, which is the case here
    }

    public void SetTier(int tier)
    {
        switch (tier)
        {
            case 1: Graphics.activeTier = UnityEngine.Rendering.GraphicsTier.Tier1; QualitySettings.SetQualityLevel(0); break;
            case 2: Graphics.activeTier = UnityEngine.Rendering.GraphicsTier.Tier2; QualitySettings.SetQualityLevel(2); break;
            case 3: Graphics.activeTier = UnityEngine.Rendering.GraphicsTier.Tier3; QualitySettings.SetQualityLevel(5); break;
            default: break;
        }
    }

    public void GetHighscore()
    {
        int highscore = 0;
        bool state = true;

        try { highscore = PlayerPrefs.GetInt("highscore"); } catch { state = false; }  

        highscoreText.text = highscore.ToString();
    }

    public void ClearHighScore()
    {
        PlayerPrefs.SetInt("highscore", 0);
        GetHighscore();
    }
    public void ToggleInsturctions()
    {
        if (PlayerPrefs.GetInt("showHelp") == 1)
        {
            PlayerPrefs.SetInt("showHelp", 0);
        }
        else
        {
            PlayerPrefs.SetInt("showHelp", 1);
        }
    }
}

public class PassiveHealth : PickUpPassive
{
    public int amount;

    public override void AddEffect(PlayerController player)
    {
        base.AddEffect(player);
        try { player.health.flashAnimation.Play("flashGreen"); } catch { }
        player.health.HitOccured(-amount);  //needs to call this function, so UI is updated... negative because hit does subtract health
    }

    public override void RemoveEffect(PlayerController player)
    {
    }
}

[RequireComponent(typeof(SphereCollider))]
public class PickUpItem : MonoBehaviour
{
    public float despawn = 60;
    private float despawnTimer = 0;

    public AudioSource PickUpSound;
    public SphereCollider interactionCollider;
    public GameObject PickUpIndicator;

    private Quaternion originRotation;
    public float rotationSpeed;
    public float offset;

    public WeaponController thisController;

    private void Start()
    {
        thisController = GetComponent<WeaponController>();
        interactionCollider = GetComponent<SphereCollider>();

        despawnTimer = despawn;

        originRotation = transform.localRotation;
    }

    private void Update()
    {
        if (interactionCollider.enabled)
        {
            despawnTimer -= Time.deltaTime;
            Vector3 added = new Vector3(0, rotationSpeed * Time.deltaTime, 0);
            transform.eulerAngles = transform.eulerAngles + added;

            if (despawnTimer <= 0)
            {
                Destroy(this.gameObject);
            }
        }
        else
        {
            despawnTimer = despawn;
            //transform.localRotation = originRotation;
        }
    }

    private void OnTriggerEnter(Collider other)         //player walks into range
    {
        PlayerController playerController = other.GetComponent<PlayerController>();     //get the players controller

        if (playerController != null)
        {
            try
            {
                if (playerController.GetComponentInChildren<WeaponController>().type == thisController.type)
                {
                    playerController.GetComponentInChildren<WeaponController>().ammo += thisController.ammo;
                    playerController.UpdateAmmoVisuals();
                    // ----soundeffect---
                    AudioSource audio = Instantiate(PickUpSound);       //play pickupsound
                    Destroy(audio.gameObject, 1);                        //destory it after 1 sec
                    Destroy(this.gameObject);
                }
                else
                {
                    playerController.inInteractionRange = true;                         //tell player that he is in range to interact
                    playerController.health.game.ShowInteraction(true);
                    playerController.interactionTrigger.AddListener(ExchangeWeapon);    //listen to instructions from the player
                }
            }
            catch
            {
                playerController.inInteractionRange = true;                         //tell player that he is in range to interact
                playerController.health.game.ShowInteraction(true);
                playerController.interactionTrigger.AddListener(ExchangeWeapon);    //listen to instructions from the player
            }
        }
    }

    private void OnTriggerExit(Collider other)                  //player leaves range
    {
        PlayerController playerController = other.GetComponent<PlayerController>();

        if (playerController != null)
        {
            playerController.inInteractionRange = false;                            //tell player that he no more is in range
            playerController.health.game.ShowInteraction(false);
            playerController.interactionTrigger.RemoveListener(ExchangeWeapon);     //dont listen to player anymore
        }
    }

    public void ExchangeWeapon(PickUpItem playersItem, PlayerController player)
    {
        // this refers to the item lying on the ground
        // playersItem refers to item in the hands of the player

        // ----soundeffect---
        AudioSource audio = Instantiate(PickUpSound);       //play pickupsound
        Destroy(audio.gameObject, 1);                        //destory it after 1 sec

        // -----exchange-----
        Transform parent = playersItem.transform.parent;            // set a reference for parent of players item
        Vector3 position = playersItem.transform.localPosition;     // set a reference for players items position
        Quaternion rotation = playersItem.transform.localRotation;  // set a reference for players items rotation 

        playersItem.transform.parent = null;                        // remove parent from players item 
        playersItem.transform.position = this.transform.position;   // set players items position to the items position on the ground               
        playersItem.transform.rotation = this.transform.rotation;   // set players items rotation to the items rotation on the ground

        playersItem.Drop();

        this.interactionCollider.enabled = false;
        this.PickUpIndicator.SetActive(false);

        player.interactionTrigger.RemoveAllListeners();             // stop all functions from listening to instructions from the player

        OnTriggerEnter(player.GetComponent<Collider>());            // simulate the item from the hands being triggered by the player

        this.transform.SetParent(parent);                           // set parent of the item on the ground to the reference set
        this.transform.localPosition = position;                    // set position of item on the ground to the reference set
        this.transform.localRotation = rotation;                    // set rotation of item on the ground to the reference set

        try { parent.GetComponent<Caller>().link.UpdateAmmoVisuals(); } catch { /*Debug.LogError(parent);*/ }
    }

    public void Drop(int ammoLeft)
    {
        thisController.animation.Stop();

        this.interactionCollider.enabled = true;
        this.PickUpIndicator.SetActive(true);
        transform.localRotation = Quaternion.identity;

        GetComponent<WeaponController>().ammo = ammoLeft;

        this.gameObject.layer = 2;      //dont respond to raycast
    }

    public void Drop()
    {
        thisController.animation.Stop();
        this.interactionCollider.enabled = true;
        this.PickUpIndicator.SetActive(true);
        transform.localRotation = Quaternion.identity;

        this.gameObject.layer = 2;
    }

    void PlaceAboveGround()
    {
        Debug.Log("to ground");

        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 4))    //draw ray down to the ground(layer 9) with a max distance of 4
        {
            Debug.Log("place down");
            transform.position = hit.point + Vector3.up * offset;
        }
        else if (Physics.Raycast(transform.position, Vector3.up, out hit, 4))    //draw ray down to the ground(layer 9) with a max distance of 4
        {
            Debug.Log("place up");
            transform.position = hit.point + Vector3.up * offset;
        }
        else
        {
            Debug.Log("no ground");
        }
    }
}

[RequireComponent(typeof(SphereCollider))]
public class PickUpPassive : MonoBehaviour
{
    public SphereCollider interactionCollider;

    public int duration = 10;

    private void Start()
    {
        interactionCollider = GetComponent<SphereCollider>();
    }

    private void OnTriggerEnter(Collider other)         //player walks into range
    {
        PlayerController playerController = other.GetComponent<PlayerController>();     //get the players controller

        if (playerController != null)
        {
            StartCoroutine(DoEffect(playerController));
        }
    }

    IEnumerator DoEffect(PlayerController player)
    {
        AddEffect(player);
        yield return new WaitForSeconds(duration);

        RemoveEffect(player);
    }

    public virtual void AddEffect(PlayerController player)
    {
        interactionCollider.enabled = false;
        try { GetComponent<MeshRenderer>().enabled = false; } catch { }
    }
    public virtual void RemoveEffect(PlayerController player)
    {
        Destroy(this.gameObject);
    }
}

public class PlayerController : MonoBehaviour
{
    public InteractionTrigger interactionTrigger;

    public Animator animator;
    public CharacterController character;
    public Transform upperBodyBone;

    public Health health;
    public HandIKControl ikHands;

    [Space]
    public float movementSpeed;
    public float rotationSpeed;
    public float upperBodyReactionRate;
    public float dampTimeOfAnimation;

    private Vector3 movement;
    public Vector3 lastKeyboardDirection;

    public float xCorrection = 0;

    [Header("Passive Parameters")]
    public bool inInteractionRange;
    public bool pickingUp = false;

    private void Start()
    {
        interactionTrigger = new InteractionTrigger();
        health.isPlayer = true;
    }

    void Update()
    {
        if (health.isDead || pickingUp) { return; }

        float x = Input.GetAxis("Horizontal");
        float y = Input.GetAxis("Vertical");
        float mouseX = Input.GetAxis("Mouse X");

        float rawX = Input.GetAxisRaw("Horizontal");
        float rawY = Input.GetAxisRaw("Vertical");

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            Vector3 lookAtPos = hit.point;
            lookAtPos.y = transform.position.y;

            Quaternion desiredRotation = Quaternion.LookRotation(lookAtPos - transform.position);       //get desired rotation by transforming direction (target to player) into a rotation
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, Time.deltaTime * upperBodyReactionRate);     //Rotate player towards the calculated rotation with a max degree   delta->smooth
        }

        if (rawX != 0 || rawY != 0)
        {
            lastKeyboardDirection = new Vector3(rawX, 0, rawY);
        }

        movement = new Vector3(x, 0, y);

        Vector3 localMovement = transform.InverseTransformVector(movement);                             //translate Vector from World to Local space
        Vector3 localKeyboard = transform.InverseTransformVector(lastKeyboardDirection);

        animator.SetFloat("offset X", localKeyboard.x, dampTimeOfAnimation, Time.deltaTime);            //send informations to Animationcontroller
        animator.SetFloat("offset Y", localKeyboard.z, dampTimeOfAnimation, Time.deltaTime);
        animator.SetFloat("movement X", localMovement.x, dampTimeOfAnimation, Time.deltaTime);
        animator.SetFloat("movement Y", localMovement.z, dampTimeOfAnimation, Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (health.isDead || pickingUp) { return; }

        if (Input.GetKey(KeyCode.E) && inInteractionRange)
        {
            pickingUp = true;
            animator.SetTrigger("PickUp");
        }
        else if (Input.GetButton("Fire1"))
        {
            try
            {
                GetComponentInChildren<WeaponController>().Fire(this.gameObject.name);

                UpdateAmmoVisuals();
            }
            catch
            {
                Debug.Log("Error when accessing Weapon");
            }
        }
    }

    private void LateUpdate()
    {
        if (health.isDead) { return; }

        Vector3 rot = transform.eulerAngles;    //get the current rotation of the body
        rot.x += xCorrection;                   //when importing from blender xyz are not the same --> so correcting it

        upperBodyBone.eulerAngles = rot;
    }

    public void AnimationEvent_ExchangeItem()
    {
        PickUpItem myItem = GetComponentInChildren<PickUpItem>();
        Debug.Log("Initialize WeaponExchange");
        interactionTrigger.Invoke(myItem, this);
    }

    public void AnimationEvent_Down()
    {
        //connect hands with item...
        WeaponController weapon = GetComponentInChildren<WeaponController>();
        ikHands.leftHandObject = weapon.LeftHandReference;
        ikHands.rightHandObject = weapon.rightHandReference;
    }

    public void AnimationEvent_PickedUp()
    {
        //Debug.Log("PickedUp");
        pickingUp = false;
        animator.ResetTrigger("PickUp");
    }

    public void UpdateAmmoVisuals()
    {
        int remainingammo = GetComponentInChildren<WeaponController>().GetRemainingAmmo();
        health.game.UpdateAmmoVisulaization(remainingammo);
    }
}

public class RocketBeahvior : MonoBehaviour
{
    public float speed;
    public float radius;

    [Space]
    public string owner;
    public Vector3 destination;
    public GameObject explosionPrefab;
    public ParticleSystem smoke;
    public float damage;

    public float yRotationCorrection;

    private void Start()
    {
        transform.Rotate(0, yRotationCorrection, 0);
    }

    private void FixedUpdate()
    {
        if (transform.position == destination)
        {
            Detonate(transform.position);
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, destination, Time.fixedDeltaTime * speed);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.name != owner)
        {
            Detonate(transform.position);
        }
    }

    private void Detonate(Vector3 point)
    {
        Physics.SyncTransforms();                                           //prevent bugs
        Collider[] colliders = Physics.OverlapSphere(point, radius);        // get all objects in radius 

        GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);       //generate explosion
        smoke.transform.SetParent(explosion.transform);
        smoke.loop = false;     // stop regenerating smoke

        foreach (Collider collider in colliders)
        {
            try
            {
                collider.transform.GetComponent<HitBehavior>().RegisterHit((int)damage);        //call hit function on object
            }catch{}
        }

        Destroy(explosion, 5f);
        Destroy(this.gameObject);
    }
}

public class Spawner : MonoBehaviour
{
    public GameController game;

    public float timeBetweenWaves = 4;
    public float maxWaveDuration = 20;
    public int maxEntities = 10;

    public GameObject[] AIs;
    public GameObject[] Bosses;

    public Transform spawnPointHolder;
    public Transform middle;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("spawn");
        StartCoroutine(WaveSpawner());
    }

    IEnumerator WaveSpawner()
    {
        int wave = 0;
        int start = Random.Range(0, spawnPointHolder.childCount);

        yield return new WaitUntil(() => GameController.isAlive);

        while (GameController.isAlive)
        {
            // Spawn Wave:
            int waveLimit = wave + 1;

            if (waveLimit % 5 == 0)    // every fifth wave is a boss level with one more every stage
            {
                int bossamount = (int)((float)waveLimit / 5f);
                SpawnBoss(bossamount, start);
            }
            else
            {
                SpawnNormal(waveLimit, start);
            }

            float timer = maxWaveDuration;
            bool wait = true;
            while (wait)
            {
                if (game.aliveEnties.childCount == 0 || timer <= 0)
                {
                    wait = false;
                }
                timer -= Time.deltaTime;

                yield return new WaitForEndOfFrame();
            }

            //yield return new WaitUntil(() => game.aliveEnties.childCount == 0);
            yield return new WaitForSeconds(timeBetweenWaves);

            wave++;
        }
        Debug.Log("Not alive");
    }

    private void SpawnNormal(int waveLimit, int start)
    {
        for (int i = 0; i < waveLimit; i++)
        {
            if (game.aliveEnties.childCount == maxEntities) { break; }

            int spawnIndex = (i + start) % spawnPointHolder.childCount;       // loop cycles throug all spawns before spawning 2 at same point
            int randomUnitIndex = Random.Range(0, AIs.Length);

            AIController ai = Instantiate(AIs[randomUnitIndex], spawnPointHolder.GetChild(spawnIndex).position, Quaternion.identity, game.aliveEnties).GetComponent<AIController>();     // spawn AI and keep reference
            ai.health.game = game;

            Vector3 direction = (middle.position - ai.transform.position) / 2;
            ai.agent.SetDestination(ai.transform.position + direction);
        }
    }

    private void SpawnBoss(int amount, int start)
    {
        for (int i = 0; i < amount; i++)
        {
            if (game.aliveEnties.childCount == maxEntities) { break; }

            int spawnIndex = (i + start) % spawnPointHolder.childCount;        // loop cycles throug all spawns before spawning 2 at same point
            int randomUnitIndex = Random.Range(0, Bosses.Length);

            AIController ai = Instantiate(Bosses[randomUnitIndex], spawnPointHolder.GetChild(spawnIndex).position, Quaternion.identity, game.aliveEnties).GetComponent<AIController>();     // spawn AI and keep reference
            ai.health.game = game;

            Vector3 direction = (middle.position - ai.transform.position) / 2;
            ai.agent.SetDestination(ai.transform.position + direction);
        }
    }
}

public class SpecialWeapon : WeaponController
{
    public GameObject rocketPrefab;

    public override bool Fire(string sender)
    {
        if (Time.fixedTime < nextShot) { return false; }
        if (ammo <= 0) { return false; }

        // Update timer to delay next shot
        nextShot = Time.fixedTime + (60.0f / rpm);
        ammo -= 1;

        try
        {
            animation.Play();
            Vector3 rotation = rayOrigin.eulerAngles;
            rotation.y -= 90;
            RocketBeahvior rocket = Instantiate(rocketPrefab, rayOrigin.position, Quaternion.Euler(rotation)).GetComponent<RocketBeahvior>();
            rocket.destination = rayOrigin.position + rayOrigin.forward * range;
            rocket.owner = sender;
            rocket.damage = damage;

        }catch { }

        try
        {
            AudioSource sound = Instantiate(gunSound); //spawn sound and destroy it after 1 sec
            Destroy(sound.gameObject, 1);
        } catch { }

        return true;
    }
}

public class WeaponController : MonoBehaviour
{
    public string type;
    public float damage = 10;
    public float rpm = 120;
    public float range;
    public float spray = 0;
    public int ammo = 120;

    public Transform muzzleFlash;
    public Transform rayVisualization;
    public AudioSource gunSound;

    public Transform rayOrigin;
    public new Animation animation;
    protected float nextShot;
    private string unbreakable = "Unbreakable";

    [Space]
    public Transform rightHandReference;
    public Transform LeftHandReference;

    void Start()
    {
        try
        {
            animation = GetComponent<Animation>();
        }catch {}
    }

    public virtual bool Fire(string sender)
    {
        if (Time.fixedTime < nextShot) { return false; }        // only continue if a certain time passed
        if (ammo <= 0) { return false; }                        // no ammo = no shot

        nextShot = Time.fixedTime + (60.0f / rpm);              // Update timer to delay next shot
        ammo -= 1;                                              // Decrease ammo by 1

        //move origin of shot forward of a value of his radius to not hit the entity itself
        Vector3 originPoint = rayOrigin.position + rayOrigin.forward * spray;

        animation.Play();                                       // animate recoil
        Transform flash = Instantiate(muzzleFlash, rayOrigin);  // Instantiate Muzzleflash
        Destroy(flash.gameObject, 0.2f);                        // CleanUp after .2 seconds

        LineRenderer line = flash.GetComponent<LineRenderer>(); // draw line
        line.SetPosition(0, rayOrigin.position);
        line.SetPosition(1, originPoint);
        line.SetPosition(2, rayOrigin.position + (rayOrigin.forward * range));

        AudioSource sound = Instantiate(gunSound);              // Spawn sound and 
        Destroy(sound.gameObject, 1);                           // clean it up after 1 sec

        // handle bulletpenetration             ((((( BUG: when rays first object is penetratable and the second one is not, it still might go through))))
        RaycastHit breakhit;
        if (Physics.Raycast(originPoint, rayOrigin.forward, out breakhit, range)) //draw ray
        {
            if (breakhit.transform.CompareTag(unbreakable))  // if hit.object is not penetratable:
            {          
                line.SetPosition(2, breakhit.point);

                ApplyHitToObject(breakhit.transform, breakhit);
                return true;                                 // dont continue this function
            }
        }

        //Calculate Ray
        RaycastHit[] hits = Physics.SphereCastAll(originPoint, spray, rayOrigin.forward, range);        // collect all hits by the same ray as done before  

        foreach (RaycastHit hit in hits) //do for all hits
        {
            Debug.DrawLine(rayOrigin.position, hit.point, Color.green, .2f);
            Debug.DrawRay(rayOrigin.position, rayOrigin.forward * hit.distance, Color.yellow, 0.05f);

            ApplyHitToObject(hit.transform, hit);
        }

        return true;
    }

    protected void ApplyHitToObject(Transform hittedObject, RaycastHit hit)
    {
        try
        {
            hittedObject.GetComponent<HitBehavior>().RegisterHit((int)damage, hit.normal, hit.point);
        }
        catch
        {
            Debug.LogWarning("Component <HitBehavior>() not found --> Object not hittable");
        }
    }

    public int GetRemainingAmmo()
    {
        return ammo;
    }
}