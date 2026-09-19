using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health - Prototype Value")]
    [SerializeField] private float _maxHealth = 100f;

    [SerializeField] private float _currentHealth;

    [Header("Death Prototype")]
    [SerializeField] private bool _disableGameplayOnDeath = true;

    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;

    public float NormalizedHealth =>
        _maxHealth <= 0f
            ? 0f
            : _currentHealth / _maxHealth;

    public bool IsDead { get; private set; }

    public event Action<float, float> HealthChanged;

    public event Action<
        float,
        GameObject
    > Damaged;

    public event Action<GameObject> Died;

    private PlayerController _playerController;
    private PlayerCameraController _cameraController;
    private PlayerInteractor _playerInteractor;
    private PlayerItemController _itemController;

    private void Awake()
    {
        _playerController =
            GetComponent<PlayerController>();

        _cameraController =
            GetComponent<PlayerCameraController>();

        _playerInteractor =
            GetComponent<PlayerInteractor>();

        _itemController =
            GetComponent<PlayerItemController>();

        InitializeHealth();
    }

    public void ReceiveDamage(
        float amount,
        GameObject source = null)
    {
        if (IsDead)
        {
            return;
        }

        if (amount <= 0f)
        {
            return;
        }

        float previousHealth =
            _currentHealth;

        _currentHealth = Mathf.Clamp(
            _currentHealth - amount,
            0f,
            _maxHealth
        );

        Damaged?.Invoke(
            amount,
            source
        );

        HealthChanged?.Invoke(
            _currentHealth,
            _maxHealth
        );

        string sourceName =
            source != null
                ? source.name
                : "Unknown";

        Debug.Log(
            $"[Health] 피해: -{amount:0.##} / " +
            $"HP {previousHealth:0.##} → " +
            $"{_currentHealth:0.##} / " +
            $"Source = {sourceName}",
            gameObject
        );

        if (_currentHealth <= 0f)
        {
            Die(source);
        }
    }

    public void RestoreHealth(float amount)
    {
        if (IsDead)
        {
            return;
        }

        if (amount <= 0f)
        {
            return;
        }

        _currentHealth = Mathf.Clamp(
            _currentHealth + amount,
            0f,
            _maxHealth
        );

        HealthChanged?.Invoke(
            _currentHealth,
            _maxHealth
        );

        Debug.Log(
            $"[Health] 회복: +{amount:0.##} / " +
            $"Current = {_currentHealth:0.##}",
            gameObject
        );
    }

    public void SetHealth(float value)
    {
        if (IsDead)
        {
            return;
        }

        _currentHealth = Mathf.Clamp(
            value,
            0f,
            _maxHealth
        );

        HealthChanged?.Invoke(
            _currentHealth,
            _maxHealth
        );

        if (_currentHealth <= 0f)
        {
            Die(null);
        }
    }

    private void InitializeHealth()
    {
        if (_maxHealth <= 0f)
        {
            _maxHealth = 1f;
        }

        _currentHealth =
            _maxHealth;

        IsDead = false;
    }

    private void Die(GameObject source)
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;
        _currentHealth = 0f;

        if (_disableGameplayOnDeath)
        {
            DisableGameplay();
        }

        Died?.Invoke(source);

        string sourceName =
            source != null
                ? source.name
                : "Unknown";

        Debug.Log(
            $"[Health] Player 사망 / " +
            $"Source = {sourceName}",
            gameObject
        );
    }

    private void DisableGameplay()
    {
        if (_playerController != null)
        {
            _playerController.CanMove = false;
            _playerController.CanSprint = false;
        }

        if (_cameraController != null)
        {
            _cameraController.CanLook = false;
        }

        if (_playerInteractor != null)
        {
            _playerInteractor.CanInteract = false;
        }

        if (_itemController != null)
        {
            _itemController.CanUseItems = false;
            _itemController.UnequipCurrentItem();
        }
    }

    private void OnValidate()
    {
        if (_maxHealth < 1f)
        {
            _maxHealth = 1f;
        }
    }
}