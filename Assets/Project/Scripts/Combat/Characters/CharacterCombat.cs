using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEditor;
using UnityEditor.EditorTools;
using Unity.Collections.Tests.CoreCLR.TestJobs;

public class CharacterCombat : MonoBehaviour
{
    private Dictionary<string, CombatCollider> hitboxes = new();
    private Dictionary<string, CombatCollider> hurtboxes = new();
    private CombatCollider blockbox;

    private Controls controls;
    private Animator animator;

    [Header("Parry Timing")]
    public float parryStartUpDelay = 0.08f;
    public float parryWindowDuration = 0.2f;

    [Header("Time Dilation")]
    public float PoweredUpBoost = 1.25f;
    public float SlowDownDebuff = 0.25f;
    private static readonly int PowerUpParam = Animator.StringToHash("PowerUp");
    private static readonly int SlowDownParam = Animator.StringToHash("SlowDown");

    [Header("VFX")]
    [SerializeField] private ParticleSystem TimeDilationDistorion;
    [SerializeField] public ParticleSystem ParryDistortion;

    public static List<CharacterCombat> Enemies = new();
    public static CharacterCombat PlayerInstance { get; private set; }

    public bool IsBlocking { get; private set; }
    private float BlockStartTime;
    public Gauge gauge;
    private void Awake()
    {
        animator = GetComponent<Animator>();
        controls = new Controls();

        controls.Player.PowerUp.performed += PowerUp;

        //distortion = GetComponentInChildren<ParticleSystem>();

        //if (distortion != null) Debug.Log("distorion not found");

        if (CompareTag("Player"))
        {
            PlayerInstance = this;
            //Debug.Log("Player Instance has been created");
        }
        else if (CompareTag("Enemy"))
        {
            Enemies.Add(this);
            Debug.Log($"{gameObject.name} has been added to the List: Enemies");
        }

        foreach (var cc in GetComponentsInChildren<CombatCollider>(true))
        {
            cc.Owner = this;
            cc.GetComponent<Collider>().enabled = false;

            switch (cc.type)
            {
                case ColliderType.Hitbox:
                    hitboxes[cc.Id] = cc;
                    break;
                case ColliderType.Hurtbox:
                    hurtboxes[cc.Id] = cc;
                    break;
                case ColliderType.Blockbox:
                    blockbox = cc;
                    break;
            }
        }

        EnableAllHurtboxes();
    }

    #region HitBox Controls
    public void Enablehitbox(string id)
    {
        Debug.Log($"[{gameObject.name}] Enablehitbox called with id: {id}");

        if (hitboxes.TryGetValue(id, out var cc))
        {
            cc.GetComponent<Collider>().enabled = true;

           // Debug.Log($"[CharacterCombat] Hitbox '{id}' collider enabled = {cc.GetComponent<Collider>().enabled}");
        }
        else
        {
            Debug.Log($"[CharacterCombat] No hitbox found with id: {id}");
        }
    }

    public void Disablehitbox(string id)
    {
        //Debug.Log($"[{gameObject.name}] Disablehitbox called with id: {id}");

        if (hitboxes.TryGetValue(id, out var cc))
        {
            cc.GetComponent<Collider>().enabled = false;
        }
    }
    #endregion

    #region Hurtbox Controls
    public void EnableAllHurtboxes()
    {
        foreach(var cc in hurtboxes.Values)
        {
            cc.GetComponent<Collider>().enabled = true;
        }
    }

    public void DisableAllHurtboxes()
    {
        foreach (var cc in hurtboxes.Values)
        {
            cc.GetComponent<Collider>().enabled = false;
        }
    }
    #endregion

    #region Blockbox Controls
    public void EnableBlockbox()
    {
        Debug.Log($"[{gameObject.name}] Blockbox Enabled");

        IsBlocking = true;
        BlockStartTime = Time.time;
        DisableAllHurtboxes();
        if (blockbox != null) blockbox.GetComponent<Collider>().enabled = true;
    }

    public void DisableBlockbox()
    {
        //Debug.Log($"[{gameObject.name}] Blockbox Disbaled");

        IsBlocking = false;
        if (blockbox != null) blockbox.GetComponent<Collider>().enabled = false;
        EnableAllHurtboxes();
    }
    #endregion 

    public bool IsInParryWindow()
    {
        float elapsed = Time.time - BlockStartTime;
        return elapsed >= parryStartUpDelay && elapsed <= (parryStartUpDelay + parryWindowDuration);
    }

    #region Time Dilation
    void PowerUp(InputAction.CallbackContext ctx)
    {
        if (!CompareTag("Player")) return;

        animator.SetFloat(PowerUpParam, PoweredUpBoost);

        TimeDilationDistorion.Play();

        ApplySlowDown();
    }

    void PowerDown()
    {
        if (CompareTag("Player"))
        {
            animator.SetFloat(PowerUpParam, 1);
        }

        RemoveSlowDown();

    }

    void ApplySlowDown()
    {
        foreach (var enemy in Enemies)
        {
            Animator EnemyAnim = enemy.GetComponent<Animator>();
            EnemyAnim.SetFloat(SlowDownParam, SlowDownDebuff);
            Debug.Log($"{enemy.name } has been Slowed Down");
        }
    }

    void RemoveSlowDown()
    {
        foreach (var enemy in Enemies)
        {
            animator.SetFloat(PowerUpParam, 1);
        }
    }
    #endregion 

    private void OnEnable()
    {
        controls.Enable();

    }

    private void OnDisable()
    {
        controls.Disable();
    }

    private void OnDestroy() => Enemies.Remove(this);
}
