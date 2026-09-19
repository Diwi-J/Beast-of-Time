using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class CharacterCombat : MonoBehaviour
{
    private Dictionary<string, CombatCollider> hitboxes = new();
    private Dictionary<string, CombatCollider> hurtboxes = new();
    private CombatCollider blockbox;

    private Controls controls;
    public Animator animator;

    private CharacterCombat enemyCombat;

    [Header("Parry Timing")]
    public float parryStartUpDelay = 0.08f;
    public float parryWindowDuration = 0.2f;

    [Header("Time Dilation")]
    public float PoweredUpBoost = 1.25f;
    public float SlowDownDebuff = 0.1f;
    private static readonly int PowerUpParam = Animator.StringToHash("PowerUp");
    private static readonly int SlowDownParam = Animator.StringToHash("SlowDown");

    public bool IsBlocking { get; private set; }
    private float BlockStartTime;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        controls = new Controls();

        controls.Player.PowerUp.performed += PowerUp;

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

            EnableAllHurtboxes();

        }
    }

    #region HitBox Controls
    public void Enablehitbox(string id)
    {
        if (hitboxes.TryGetValue(id, out var cc))
        {
            cc.GetComponent<Collider>().enabled = true;
        }
    }

    public void Disablehitbox(string id)
    {
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
        IsBlocking = true;
        BlockStartTime = Time.time;
        DisableAllHurtboxes();
        if (blockbox != null) blockbox.GetComponent<Collider>().enabled = true;
    }

    public void DisableBlockbox()
    {
        IsBlocking = false;
        if (blockbox != null) blockbox.GetComponent<Collider>().enabled = false;
        EnableAllHurtboxes();
    }
    #endregion 

    public bool IsInParryWindow()
    {
        float elapsed = Time.time - BlockStartTime;
        return elapsed >= parryStartUpDelay && elapsed <= (parryStartUpDelay - parryWindowDuration);
    }

    #region Time Dilation
    void SetAnimationSpeed(string paramater, float multiplier)
    {
        animator.SetFloat(paramater, multiplier);
    }

    void PowerUp(InputAction.CallbackContext ctx)
    {
        animator.SetFloat(PowerUpParam, PoweredUpBoost);
        if (enemyCombat != null)
            enemyCombat.animator.SetFloat(SlowDownParam, SlowDownDebuff);
    }

    void PowerDown()
    {
        animator.SetFloat(PowerUpParam, 1f);
        if (enemyCombat != null)
            enemyCombat.animator.SetFloat(SlowDownParam, 1f);
    }
    #endregion 

    private void OnEnable()
    {
        controls.Enable();
        controls.Player.PowerUp.performed += PowerUp;
    }

    void OnDisable()
    {
        controls.Disable();
        controls.Player.PowerUp.performed -= PowerUp;
    }
}
