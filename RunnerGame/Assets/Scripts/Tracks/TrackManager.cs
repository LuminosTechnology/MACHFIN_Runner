using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.AddressableAssets;
using UnityEngine.Analytics;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using GameObject = UnityEngine.GameObject;
using System.Linq;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif

/// <summary>
/// The TrackManager handles creating track segments, moving them and handling the whole pace of the game.
/// 
/// The cycle is as follows:
/// - Begin is called when the game starts.
///     - if it's a first run, init the controller, collider etc. and start the movement of the track.
///     - if it's a rerun (after watching ads on GameOver) just restart the movement of the track.
/// - Update moves the character and - if the character reaches a certain distance from origin (given by floatingOriginThreshold) -
/// moves everything back by that threshold to "reset" the player to the origin. This allow to avoid floating point error on long run.
/// It also handles creating the tracks segements when needed.
/// 
/// If the player has no more lives, it pushes the GameOver state on top of the GameState without removing it. That way we can just go back to where
/// we left off if the player watches an ad and gets a second chance. If the player quits, then:
/// 
/// - End is called and everything is cleared and destroyed, and we go back to the Loadout State.
/// </summary>
public class TrackManager : MonoBehaviour
{
    static public TrackManager instance
    {
        get { return s_Instance; }
    }

    static protected TrackManager s_Instance;

    static int s_StartHash = Animator.StringToHash("Start");

    public delegate int MultiplierModifier(int current);

    public MultiplierModifier modifyMultiply;

    [Header("Character & Movements")] public CharacterInputController characterController;
    public float minSpeed = 5.0f;
    public float maxSpeed = 10.0f;
    public int speedStep = 4;
    public float laneOffset = 1.0f;

    public int minLineLength = 5;
    public int maxLineLength = 15;
    private float increment = 1.5f;
    public float gapOffset = 5f;


    public bool invincible = false;

    [Header("Objects")] public ConsumableDatabase consumableDatabase;
    public MeshFilter skyMeshFilter;
     
    [SerializeField] private LayerMask _obstacleLayerMask = 1 << 9;


    [Header("Parallax")] public Transform parallaxRoot;
    public float parallaxRatio = 0.5f;

    [Header("Tutorial")] public ThemeData tutorialThemeData;
    [Header("Segment")] public float segmentCheckRadius = 7f;


    public System.Action<TrackSegment> newSegmentCreated;
    public System.Action<TrackSegment> currentSegementChanged;

    public int trackSeed
    {
        get { return m_TrackSeed; }
        set { m_TrackSeed = value; }
    }

    public float timeToStart
    {
        get { return m_TimeToStart; }
    } // Will return -1 if already started (allow to update UI)

    public int score
    {
        get { return m_Score; }
    }

    public int multiplier
    {
        get { return m_Multiplier; }
    }

    public float currentSegmentDistance
    {
        get { return m_CurrentSegmentDistance; }
    }

    public float worldDistance
    {
        get { return m_TotalWorldDistance; }
    }

    public float speed
    {
        get { return m_Speed; }
    }

    public float speedRatio
    {
        get { return (m_Speed - minSpeed) / (maxSpeed - minSpeed); }
    }

    public int currentZone
    {
        get { return m_CurrentZone; }
    }

    public TrackSegment currentSegment
    {
        get { return m_Segments[0]; }
    }

    public List<TrackSegment> segments
    {
        get { return m_Segments; }
    }

    public ThemeData currentTheme
    {
        get { return m_CurrentThemeData; }
    }

    public bool isMoving
    {
        get { return m_IsMoving; }
    }

    public bool isRerun
    {
        get { return m_Rerun; }
        set { m_Rerun = value; }
    }

    public bool isTutorial
    {
        get { return m_IsTutorial; }
        set { m_IsTutorial = value; }
    }

    public bool isLoaded { get; set; }

    //used by the obstacle spawning code in the tutorial, as it need to spawn the 1st obstacle in the middle lane
    public bool firstObstacle { get; set; }

    protected float m_TimeToStart = -1.0f;

    // If this is set to -1, random seed is init to system clock, otherwise init to that value
    // Allow to play the same game multiple time (useful to make specific competition/challenge fair between players)
    protected int m_TrackSeed = -1;

    protected float m_CurrentSegmentDistance;
    protected float m_TotalWorldDistance;
    protected bool m_IsMoving;
    protected float m_Speed;

    protected float m_TimeSincePowerup; // The higher it goes, the higher the chance of spawning one
    protected float m_TimeSinceLastPremium;

    protected int m_Multiplier;

    protected List<TrackSegment> m_Segments = new List<TrackSegment>();
    protected List<TrackSegment> m_PastSegments = new List<TrackSegment>();
    protected int m_SafeSegementLeft;

    protected ThemeData m_CurrentThemeData;
    protected int m_CurrentZone;
    protected float m_CurrentZoneDistance;
    protected int m_PreviousSegment = -1;

    protected int m_Score;
    protected float m_ScoreAccum;

    protected bool
        m_Rerun; // This lets us know if we are entering a game over (ads) state or starting a new game (see GameState)

    protected bool
        m_IsTutorial; //Tutorial is a special run that don't chance section until the tutorial step is "validated" by the TutorialState.

    Vector3 m_CameraOriginalPos = Vector3.zero;

    private Camera m_camera;

    const float k_FloatingOriginThreshold = 10000f;

    protected const float k_CountdownToStartLength = 5f;
    protected const float k_CountdownSpeed = 1.5f;
    protected const float k_StartingSegmentDistance = 2f;
    protected const int k_StartingSafeSegments = 2;
    protected const int k_StartingCoinPoolSize = 256;
    protected const int k_DesiredSegmentCount = 10;
    protected const float k_SegmentRemovalDistance = -30f;
    protected const float k_Acceleration = 0.2f;

    [Header("Camera Settings")] [SerializeField]
    private float _cameraFOV = 80f;
    [SerializeField] private float _cameraAngle = 20;
    [SerializeField] private float _cameraDistance = -4.5f;
    [SerializeField] private float _cameraHeight = 4.5f;

    protected void Awake()
    {
        m_ScoreAccum = 0.0f;
        s_Instance = this;
    }

    public void StartMove(bool isRestart = true)
    {
        characterController.StartMoving();
        m_IsMoving = true;
        if (isRestart)
            m_Speed = minSpeed;
    }

    public void StopMove()
    {
        m_IsMoving = false;
    }

    IEnumerator WaitToStart()
    {
        characterController.character.animator.Play(s_StartHash);
        float length = k_CountdownToStartLength;
        m_TimeToStart = length;

        while (m_TimeToStart >= 0)
        {
            yield return null;
            m_TimeToStart -= Time.deltaTime * k_CountdownSpeed;
        }

        m_TimeToStart = -1;

        if (m_Rerun)
        {
            // Make invincible on rerun, to avoid problems if the character died in front of an obstacle
            characterController.characterCollider.SetInvincible();
        }

        characterController.StartRunning();
        StartMove();
    }

    public IEnumerator Begin()
    {
        if (!m_Rerun)
        {
            firstObstacle = true;
            m_camera = Camera.main;
            m_CameraOriginalPos = m_camera.transform.position;

            if (m_TrackSeed != -1)
                Random.InitState(m_TrackSeed);
            else
                Random.InitState((int)System.DateTime.Now.Ticks);

            // Since this is not a rerun, init the whole system (on rerun we want to keep the states we had on death)
            m_CurrentSegmentDistance = k_StartingSegmentDistance;
            m_TotalWorldDistance = 0.0f;

            characterController.gameObject.SetActive(true);

            //Addressables 1.0.1-preview
            // Spawn the player
            var op = Addressables.InstantiateAsync(PlayerData.instance.characters[PlayerData.instance.usedCharacter],
                Vector3.zero,
                Quaternion.identity);
            yield return op;
            if (op.Result == null || !(op.Result is GameObject))
            {
                Debug.LogWarning(string.Format("Unable to load character {0}.",
                    PlayerData.instance.characters[PlayerData.instance.usedCharacter]));
                yield break;
            }

            Character player = op.Result.GetComponent<Character>();

            player.SetupAccesory(PlayerData.instance.usedAccessory);

            characterController.character = player;
            characterController.trackManager = this;

            characterController.Init();
            characterController.CheatInvincible(invincible);

            //Instantiate(CharacterDatabase.GetCharacter(PlayerData.instance.characters[PlayerData.instance.usedCharacter]), Vector3.zero, Quaternion.identity);
            player.transform.SetParent(characterController.characterCollider.transform, false);
            m_camera.transform.SetParent(characterController.transform, true);

            if (m_IsTutorial)
                m_CurrentThemeData = tutorialThemeData;
            else
            {
                m_CurrentThemeData =
                    ThemeDatabase.GetThemeData(PlayerData.instance.themes[PlayerData.instance.usedTheme]);
            }

            m_CurrentZone = 0;
            m_CurrentZoneDistance = 0;
            
            //Camera
             
            m_camera.transform.position = m_CameraOriginalPos;

            m_camera.transform.rotation = Quaternion.Euler(new Vector3(_cameraAngle, 0,0));
            m_camera.transform.position = new Vector3(0, _cameraHeight, _cameraDistance);
            m_camera.fieldOfView = _cameraFOV;
            
            if (skyMeshFilter != null)
            {
                // Debug.Log(m_CurrentThemeData);
                if (m_CurrentThemeData.skyMesh != null)
                    skyMeshFilter.sharedMesh = m_CurrentThemeData.skyMesh;
                else
                {
                    Debug.LogError($"No sky mesh found for theme {m_CurrentThemeData.themeName}");
                }
            }

            RenderSettings.fogColor = m_CurrentThemeData.fogColor;
            RenderSettings.fog = true;

            gameObject.SetActive(true);
            characterController.gameObject.SetActive(true);
            characterController.picanhas = 0;
            characterController.premium = 0;

            m_Score = 0;
            m_ScoreAccum = 0;

            m_SafeSegementLeft = m_IsTutorial ? 0 : k_StartingSafeSegments;
            if (!m_IsTutorial)
            {
                Coin.coinPool = new Pooler(currentTheme.collectiblePrefab, k_StartingCoinPoolSize);
                int _index = 0;
                Coin.coinsPool = new Pooler[currentTheme.collectiblesData.Length];

                foreach (var coin in currentTheme.collectiblesData)
                {
                    // Debug.Log($"Creating {coin.m_name} pool");
                    Coin.coinsPool[_index] = new Pooler(coin.m_CollectiblePrefab, k_StartingCoinPoolSize);
                    _index++;
                }

                PlayerData.instance.StartRunMissions(this);
            }


#if UNITY_ANALYTICS
            AnalyticsEvent.GameStart(new Dictionary<string, object>
            {
                { "theme", m_CurrentThemeData.themeName},
                { "character", player.characterName },
                { "accessory",  PlayerData.instance.usedAccessory >= 0 ? player.accessories[PlayerData.instance.usedAccessory].accessoryName : "none"}
            });
#endif
        }

        characterController.Begin();
        StartCoroutine(WaitToStart());
        isLoaded = true;
    }

    public void End()
    {
        Console.WriteLine($"[TrackManager.End Line 383] - END TRACK");
        foreach (TrackSegment seg in m_Segments)
        {
            Addressables.ReleaseInstance(seg.gameObject);
            _spawnedSegments--;
        }

        for (int i = 0; i < m_PastSegments.Count; ++i)
        {
            Addressables.ReleaseInstance(m_PastSegments[i].gameObject);
        }

        m_Segments.Clear();
        m_PastSegments.Clear();

        characterController.End();

        gameObject.SetActive(false);
        Addressables.ReleaseInstance(characterController.character.gameObject);
        characterController.character = null;

        m_camera.transform.SetParent(null); 

        characterController.gameObject.SetActive(false);

        for (int i = 0; i < parallaxRoot.childCount; ++i)
        {
            _parallaxRootChildren--;
            Destroy(parallaxRoot.GetChild(i).gameObject);
        }

        //if our consumable wasn't used, we put it back in our inventory
        if (characterController.inventory != null)
        {
            PlayerData.instance.Add(characterController.inventory.GetConsumableType());
            characterController.inventory = null;
        }
    }


    private int _parallaxRootChildren = 0;
    private int _spawnedSegments = 0;

    void Update()
    {
        while (_spawnedSegments < (m_IsTutorial ? 4 : k_DesiredSegmentCount))
        {
            StartCoroutine(SpawnNewSegment());
            _spawnedSegments++;
        }

        if (parallaxRoot != null && currentTheme.cloudPrefabs.Length > 0)
        {
            while (_parallaxRootChildren < currentTheme.cloudNumber)
            {
                float lastZ = parallaxRoot.childCount == 0
                    ? 0
                    : parallaxRoot.GetChild(parallaxRoot.childCount - 1).position.z +
                      currentTheme.cloudMinimumDistance.z;

                GameObject cloud = currentTheme.cloudPrefabs[Random.Range(0, currentTheme.cloudPrefabs.Length)];
                if (cloud != null)
                {
                    GameObject obj = Instantiate(cloud);
                    obj.transform.SetParent(parallaxRoot, false);

                    obj.transform.localPosition =
                        Vector3.up * (currentTheme.cloudMinimumDistance.y +
                                      (Random.value - 0.5f) * currentTheme.cloudSpread.y)
                        + Vector3.forward * (lastZ + (Random.value - 0.5f) * currentTheme.cloudSpread.z)
                        + Vector3.right * (currentTheme.cloudMinimumDistance.x +
                                           (Random.value - 0.5f) * currentTheme.cloudSpread.x);

                    obj.transform.localScale = obj.transform.localScale * (1.0f + (Random.value - 0.5f) * 0.5f);
                    obj.transform.localRotation = Quaternion.AngleAxis(Random.value * 360.0f, Vector3.up);
                    _parallaxRootChildren++;
                }
            }
        }

        if (!m_IsMoving)
            return;

        float scaledSpeed = m_Speed * Time.deltaTime;
        m_ScoreAccum += scaledSpeed;
        m_CurrentZoneDistance += scaledSpeed;

        int intScore = Mathf.FloorToInt(m_ScoreAccum);
        if (intScore != 0) AddScore(intScore);
        m_ScoreAccum -= intScore;

        m_TotalWorldDistance += scaledSpeed;
        m_CurrentSegmentDistance += scaledSpeed;

        if (m_CurrentSegmentDistance > m_Segments[0].worldLength)
        {
            m_CurrentSegmentDistance -= m_Segments[0].worldLength;

            // m_PastSegments are segment we already passed, we keep them to move them and destroy them later 
            // but they aren't part of the game anymore 
            m_PastSegments.Add(m_Segments[0]);
            m_Segments.RemoveAt(0);
            _spawnedSegments--;

            if (currentSegementChanged != null) currentSegementChanged.Invoke(m_Segments[0]);
        }

        Vector3 currentPos;
        Quaternion currentRot;
        Transform characterTransform = characterController.transform;

        m_Segments[0].GetPointAtInWorldUnit(m_CurrentSegmentDistance, out currentPos, out currentRot);


        // Floating origin implementation
        // Move the whole world back to 0,0,0 when we get too far away.
        bool needRecenter = currentPos.sqrMagnitude > k_FloatingOriginThreshold;

        // Parallax Handling
        if (parallaxRoot != null)
        {
            Vector3 difference = (currentPos - characterTransform.position) * parallaxRatio;
            ;
            int count = parallaxRoot.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform cloud = parallaxRoot.GetChild(i);
                cloud.position += difference - (needRecenter ? currentPos : Vector3.zero);
            }
        }

        if (needRecenter)
        {
            int count = m_Segments.Count;
            for (int i = 0; i < count; i++)
            {
                m_Segments[i].transform.position -= currentPos;
            }

            count = m_PastSegments.Count;
            for (int i = 0; i < count; i++)
            {
                m_PastSegments[i].transform.position -= currentPos;
            }

            // Recalculate current world position based on the moved world
            m_Segments[0].GetPointAtInWorldUnit(m_CurrentSegmentDistance, out currentPos, out currentRot);
        }

        characterTransform.rotation = currentRot;
        characterTransform.position = currentPos;

        if (parallaxRoot != null && currentTheme.cloudPrefabs.Length > 0)
        {
            for (int i = 0; i < parallaxRoot.childCount; ++i)
            {
                Transform child = parallaxRoot.GetChild(i);

                // Destroy unneeded clouds
                if ((child.localPosition - currentPos).z < -50)
                {
                    _parallaxRootChildren--;
                    Destroy(child.gameObject);
                }
            }
        }

        // Still move past segment until they aren't visible anymore.
        for (int i = 0; i < m_PastSegments.Count; ++i)
        {
            if ((m_PastSegments[i].transform.position - currentPos).z < k_SegmentRemovalDistance)
            {
                m_PastSegments[i].Cleanup();
                m_PastSegments.RemoveAt(i);
                i--;
            }
        }

        PowerupSpawnUpdate();

        if (!m_IsTutorial)
        {
            if (m_Speed < maxSpeed)
                m_Speed += k_Acceleration * Time.deltaTime;
            else
                m_Speed = maxSpeed;
        }

        m_Multiplier = 1 + Mathf.FloorToInt((m_Speed - minSpeed) / (maxSpeed - minSpeed) * speedStep);

        if (modifyMultiply != null)
        {
            foreach (MultiplierModifier part in modifyMultiply.GetInvocationList())
            {
                m_Multiplier = part(m_Multiplier);
            }
        }

        if (!m_IsTutorial)
        {
            //check for next rank achieved
            int currentTarget = (PlayerData.instance.rank + 1) * 300;
            if (m_TotalWorldDistance > currentTarget)
            {
                PlayerData.instance.rank += 1;
                PlayerData.instance.Save();
#if UNITY_ANALYTICS
//"level" in our game are milestone the player have to reach : one every 300m
            AnalyticsEvent.LevelUp(PlayerData.instance.rank);
#endif
            }

            PlayerData.instance.UpdateMissions(this);
        }

        MusicPlayer.instance.UpdateVolumes(speedRatio);
    }

    public void PowerupSpawnUpdate()
    {
        m_TimeSincePowerup += Time.deltaTime;
        m_TimeSinceLastPremium += Time.deltaTime;
    }

    public void ChangeZone()
    {
        m_CurrentZone += 1;
        if (m_CurrentZone >= m_CurrentThemeData.zones.Length)
            m_CurrentZone = 0;

        m_CurrentZoneDistance = 0;
    }

    private readonly Vector3 _offScreenSpawnPos = new Vector3(-100f, -100f, -100f);

    public bool IsObstacleAreaFree(TrackSegment segment, float obstacleT)
    {
        // Pega o ponto exato baseado no t da curva (mesma lógica do spawn atual)
        Vector3 pos;
        Quaternion rot;
        segment.GetPointAt(obstacleT, out pos, out rot);

        float radius = segment.obstacleCheckRadius;

        // Colisão apenas com obstaculos 

        // Verificar se já existe obstáculo na área
        bool hasObstacle = Physics.CheckSphere(pos, radius, _obstacleLayerMask);

        return !hasObstacle;
    }

    public bool IsSegmentAreaFree(Vector3 position, float radius)
    {
        LayerMask mask = LayerMask.GetMask("TrackSegment");

        bool hasSomething = Physics.CheckSphere(position, radius, mask);

        return !hasSomething;
    }

    public IEnumerator SpawnNewSegment()
    {
        if (!m_IsTutorial)
        {
            if (m_CurrentThemeData.zones[m_CurrentZone].length < m_CurrentZoneDistance)
                ChangeZone();
        }

        int attempts = 5; // segurança: tenta várias vezes encontrar um segmento que caiba

        TrackSegment newSegment = null;

        while (attempts > 0)
        {
            attempts--;

            int segmentUse = Random.Range(0, m_CurrentThemeData.zones[m_CurrentZone].prefabList.Length);

            if (segmentUse == m_PreviousSegment)
                segmentUse = (segmentUse + 1) % m_CurrentThemeData.zones[m_CurrentZone].prefabList.Length;


            // ==== Carrega o segmento sem posicionar ainda ====
            AsyncOperationHandle segmentOp =
                m_CurrentThemeData.zones[m_CurrentZone].prefabList[segmentUse]
                    .InstantiateAsync(_offScreenSpawnPos, Quaternion.identity);

            yield return segmentOp;

            if (segmentOp.Result == null)
            {
                Debug.LogWarning("Failed to load segment.");
                continue;
            }

            GameObject segmentObj = segmentOp.Result as GameObject;
            newSegment = segmentObj.GetComponent<TrackSegment>();

            // =====================
            // CALCULA POSIÇÃO REAL
            // =====================

            Vector3 currentExitPoint;
            Quaternion currentExitRotation;

            if (m_Segments.Count > 0)
                m_Segments[m_Segments.Count - 1].GetPointAt(1f, out currentExitPoint, out currentExitRotation);
            else
            {
                currentExitPoint = transform.position;
                currentExitRotation = transform.rotation;
            }

            newSegment.transform.rotation = currentExitRotation;

            Vector3 entryPoint;
            Quaternion entryRotation;

            newSegment.GetPointAt(0f, out entryPoint, out entryRotation);

            Vector3 finalPos = currentExitPoint + (newSegment.transform.position - entryPoint);

            // ========================
            // CHECAR COLISÃO DE ÁREA
            // ========================

            if (!IsSegmentAreaFree(finalPos, segmentCheckRadius))
            {
                Debug.Log("Segmento bloqueado — tentando outro...");

                // IMPORTANTE: descartar o segmento carregado
                Addressables.ReleaseInstance(segmentObj);

                newSegment = null;
                continue; // tenta outro prefab
            }

            // Área está livre: posicionar o segmento
            newSegment.transform.position = finalPos;
            break;
        }

        // Se ainda assim não achou um segmento válido:
        if (newSegment == null)
        {
            Debug.LogError("Nenhum segmento pôde ser spawnado em área livre.");
            yield break;
        }

        // =========================
        // SEGMENTO ACEITO — FINALIZA
        // =========================

        newSegment.manager = this;

        // newSegment.transform.localScale = new Vector3((Random.value > 0.5f ? -1 : 1), 1, 1);
        newSegment.objectRoot.localScale = new Vector3(1f / newSegment.transform.localScale.x, 1, 1);

        if (m_SafeSegementLeft <= 0)
            SpawnObstacle(newSegment);
        else
            m_SafeSegementLeft--;

        m_Segments.Add(newSegment);

        newSegmentCreated?.Invoke(newSegment);
    }

    public void SpawnObstacle(TrackSegment segment)
    {
        if (!segment.hasObstacles) return;
        if (segment.possibleObstacles.Length != 0)
        {
            for (int i = 0; i < segment.obstaclePositions.Length; ++i)
            {
                AssetReference assetRef = segment.possibleObstacles[Random.Range(0, segment.possibleObstacles.Length)];
                // if (!IsObstacleAreaFree(segment, i))
                // {
                //     ClearSpawnArea(segment,i);
                // }

                StartCoroutine(SpawnFromAssetReference(assetRef, segment, i));
            }
        }

        StartCoroutine(SpawnCoinAndPowerup(segment));
    }

    public void ClearSpawnArea(TrackSegment segment, float obstacleT)
    {
        Vector3 pos;
        Quaternion rot;
        segment.GetPointAt(obstacleT, out pos, out rot );

        float radius = segment.obstacleCheckRadius;
        
        Collider[] obstacles =  Physics.OverlapSphere(pos, radius);
        foreach (Collider obstacle in obstacles)
        {
            Destroy(obstacle.gameObject);
        }
    }

    private IEnumerator SpawnFromAssetReference(AssetReference reference, TrackSegment segment, int posIndex)
    {
        AsyncOperationHandle op = Addressables.LoadAssetAsync<GameObject>(reference);
        yield return op;
        GameObject obj = op.Result as GameObject;
        if (obj != null)
        {
            Obstacle obstacle = obj.GetComponent<Obstacle>();
            if (obstacle != null)
                yield return obstacle.Spawn(segment, segment.obstaclePositions[posIndex]);
        }
    }

    public IEnumerator SpawnCoinAndPowerup(TrackSegment segment)
    {
        if (!m_IsTutorial)
        {
            float currentWorldPos = 0.0f;
            int currentLane = Random.Range(0, 3);

            float powerupChance = Mathf.Clamp01(Mathf.Floor(m_TimeSincePowerup) * 0.5f * 0.001f);
            float premiumChance = Mathf.Clamp01(Mathf.Floor(m_TimeSinceLastPremium) * 0.5f * 0.0001f);

            while (currentWorldPos < segment.worldLength)
            {
                // Gera um comprimento aleatório para a linha de moedas e uma lacuna.


                // Verifica se deve gerar um power-up ou um item premium.
                if (Random.value < powerupChance)
                {
                    // Lógica para gerar power-up (mantida do original para um único item).
                    // ... (você pode adicionar a lógica de spawn de power-up aqui se desejar)
                    m_TimeSincePowerup = 0.0f;
                }
                else if (Random.value < premiumChance)
                {
                    // Lógica para gerar item premium (mantida do original para um único item).
                    // ... (você pode adicionar a lógica de spawn de item premium aqui se desejar)
                    m_TimeSinceLastPremium = 0.0f;
                }
                else
                {
                    // int lineLength = Random.Range(minLineLength, maxLineLength); 


                    CollectibleCurrency chosen = GetRandomCollectible();
                    Pooler pool = Coin.coinsPool.First(p => p.m_Original == chosen.m_CollectiblePrefab);
                    // Gera uma linha de moedas.
                    int lineLength = Random.Range(chosen.m_MinLineLength, chosen.m_MaxLineLength);
                    increment = chosen.m_Increment;


                    // Muda de faixa aleatoriamente.
                    if (Random.value < 0.1f)
                        currentLane = (currentLane + Random.Range(1, 3)) % 3;

                    for (int i = 0; i < lineLength; ++i)
                    {
                        Vector3 pos;
                        Quaternion rot;
                        segment.GetPointAtInWorldUnit(currentWorldPos, out pos, out rot);

                        // Muda de faixa aleatoriamente.

                        pos = pos + ((currentLane - 1) * laneOffset * (rot * Vector3.right));
                        // --- INÍCIO DA VERIFICAÇÃO ---
                        // Verifica se há colisoes numa esfera nessa posição.
                        // Se o array retornado tiver comprimento 0, o espaço está livre.
                        if (Physics.OverlapSphere(pos, 0.5f,_obstacleLayerMask).Length == 0)
                        {
                            // Espaço livre -> Pode spawnar
                            GameObject toUse = pool.Get(pos, rot);
                            toUse.GetComponent<Coin>().poolOrigin = pool;
                            toUse.transform.SetParent(segment.collectibleTransform, true);
                        }
                        else 
                        {
                            // Opcional: Log para debug se necessário
                            // Debug.Log("Tentou spawnar moeda em cima de algo e foi impedido.");
                        }
                        // --- FIM DA VERIFICAÇÃO ---
 

                        currentWorldPos += increment;
                    }
                }

                currentWorldPos += increment * gapOffset; // Adiciona uma lacuna após a linha de moedas ou power-up.
            }
        }

        yield return null;
    }

    public CollectibleCurrency GetRandomCollectible()
    {
        float total = 0f;

        foreach (var coin in currentTheme.collectiblesData)
        {
            total += coin.m_SpawnChance;
        }

        float rand = Random.Range(0, total);
        total = 0;
        foreach (var coin in currentTheme.collectiblesData)
        {
            if (rand < coin.m_SpawnChance)
            {
                return coin;
            }

            rand -= coin.m_SpawnChance;
        }

        return currentTheme.collectiblesData[0];
    }

    public void AddScore(int amount)
    {
        int finalAmount = amount;
        m_Score += finalAmount * m_Multiplier;
    }
}