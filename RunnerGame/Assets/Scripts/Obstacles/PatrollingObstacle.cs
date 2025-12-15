using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Random = UnityEngine.Random;

public class PatrollingObstacle : Obstacle
{
    static int s_SpeedRatioHash = Animator.StringToHash("SpeedRatio");
    static int s_DeadHash = Animator.StringToHash("Dead");

    [Tooltip("Minimum time to cross all lanes.")]
    public float minTime = 2f;

    [Tooltip("Maximum time to cross all lanes.")]
    public float maxTime = 5f;

    [Tooltip("Leave empty if no animation")]
    public Animator animator;

    public AudioClip[] patrollingSound;

    protected TrackSegment m_Segment;

    protected Vector3 m_OriginalPosition = Vector3.zero;
    protected float m_MaxSpeed;
    protected float m_CurrentPos;

    protected AudioSource m_Audio;
    private bool m_isMoving = false;

    public Vector3 boxSize = new Vector3(2, 1, 1);

    protected const float k_LaneOffsetToFullWidth = 2f;

    public override IEnumerator Spawn(TrackSegment segment, float t)
    {
        Vector3 position;
        Quaternion rotation;
        segment.GetPointAt(t, out position, out rotation);


        // --- INÍCIO DA ALTERAÇÃO ---
        // Define a LayerMask para a layer 8 (1 << 8)
        int obstacleLayerMask = 1 << 8;

        // Raio de verificação. Ajuste este valor (1.0f) conforme o tamanho do seu obstáculo.
        // Se o obstáculo for grande, aumente; se for pequeno, diminua.

        // Verifica se há colisores na posição de spawn dentro da layer especificada
        var cast = Physics.OverlapBox(position, boxSize, Quaternion.identity, obstacleLayerMask);
        if (cast.Length > 0)
        {
            // Se encontrou algo, cancela o spawn imediatamente
            foreach (var o in cast)
            {
                // Debug.Log(o.transform.name);
                    // Addressables.ReleaseInstance(o.gameObject);
                Destroy(o.gameObject);
            }
            

            // Debug.Log("Spawn bloqueado: Objeto detectado na layer 8."); // Descomente para debug
            // yield break;
        }
        //

        AsyncOperationHandle op = Addressables.InstantiateAsync(gameObject.name, position, rotation);
        yield return op;
        if (op.Result == null || !(op.Result is GameObject))
        {
            Debug.LogWarning(string.Format("Unable to load obstacle {0}.", gameObject.name));
            yield break;
        }

        GameObject obj = op.Result as GameObject;

        obj.transform.SetParent(segment.objectRoot, true);

        PatrollingObstacle po = obj.GetComponent<PatrollingObstacle>();
        po.m_Segment = segment;

        //TODO : remove that hack related to #issue7
        Vector3 oldPos = obj.transform.position;
        obj.transform.position += Vector3.back;
        obj.transform.position = oldPos;

        po.Setup();
    }

    public override void Setup()
    {
        m_Audio = GetComponent<AudioSource>();
        if (m_Audio != null && patrollingSound != null && patrollingSound.Length > 0)
        {
            m_Audio.loop = true;
            m_Audio.clip = patrollingSound[Random.Range(0, patrollingSound.Length)];
            m_Audio.Play();
        }

        m_OriginalPosition = transform.localPosition + transform.right * m_Segment.manager.laneOffset;
        transform.localPosition = m_OriginalPosition;

        float actualTime = Random.Range(minTime, maxTime);

        //time 2, becaus ethe animation is a back & forth, so we need the speed needed to do 4 lanes offset in the given time
        m_MaxSpeed = (m_Segment.manager.laneOffset * k_LaneOffsetToFullWidth * 2) / actualTime;

        if (animator != null)
        {
            AnimationClip clip = animator.GetCurrentAnimatorClipInfo(0)[0].clip;
            animator.SetFloat(s_SpeedRatioHash, clip.length / actualTime);
        }

        m_isMoving = true;
    }

    public override void Impacted()
    {
        m_isMoving = false;
        base.Impacted();

        if (animator != null)
        {
            animator.SetTrigger(s_DeadHash);
        }
    }

    void Update()
    {
        if (!m_isMoving)
            return;

        m_CurrentPos += Time.deltaTime * m_MaxSpeed;

        transform.localPosition = m_OriginalPosition - transform.right *
            Mathf.PingPong(m_CurrentPos, m_Segment.manager.laneOffset * k_LaneOffsetToFullWidth);
    }
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 _position = transform.position;
        Gizmos.DrawWireCube(_position, boxSize);
    }
    #endif
}