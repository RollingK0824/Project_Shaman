using System.Collections.Generic;
using Mirror;
using UnityEngine;

// Owns local UI instances. Their visibility policies remain independent.
public class PlayerLocalUIBootstrap : MonoBehaviour
{
    [Header("Stress HUD")]
    [SerializeField] private PlayerStressHUD _hudPrefab;
    [SerializeField] private PlayerStressController _stress;
    [SerializeField] private PlayerHealth _health;
    [Header("Crosshair")]
    [SerializeField] private PlayerCrosshair _crosshairPrefab;

    [Header("Notebook Pages")]
    [SerializeField] private CraftingPanelUI _craftingPanelPrefab;
    [SerializeField] private List<CraftingRecipeData> _craftingRecipes = new();
    public CraftingPanelUI CraftingPanelPrefab => _craftingPanelPrefab;
    public IReadOnlyList<CraftingRecipeData> CraftingRecipes
    {
        get
        {
            // Also runs on the server before its first request. Preserve serialized
            // indices (including retired recipes) used by NetPlayerCrafting.
            if (!_recipesReady)
            {
                _recipesReady = true;
                AddShippedRecipe("RegistryTalismanRecipe");
                AddShippedRecipe("BarrierTalismanRecipe");
            }
            return _craftingRecipes;
        }
    }
    private bool _recipesReady;
    private void AddShippedRecipe(string name)
    {
        var recipe = Resources.Load<CraftingRecipeData>("PlayerCrafting/" + name);
        if (recipe != null && !_craftingRecipes.Contains(recipe)) _craftingRecipes.Add(recipe);
    }

    [SerializeField] private GameObject _notebookPrefab;
    [SerializeField] private InventoryPanelUI _inventoryPanelPrefab;
    public InventoryPanelUI InventoryPanelPrefab => _inventoryPanelPrefab;
    [SerializeField] private QuestItemCatalog _questItemCatalog;
    public QuestItemCatalog QuestItemCatalog => _questItemCatalog;
    [SerializeField] private NotebookScheduleView _villagerSchedulePrefab;
    public NotebookScheduleView VillagerSchedulePrefab => _villagerSchedulePrefab;
    [SerializeField] private Sprite _notebookHUDIcon;
    public Sprite NotebookHUDIcon => _notebookHUDIcon;
    private PlayerNotebookUI _notebookUI;
    private bool _ownsNotebook;

    private NetworkIdentity _identity;
    private PlayerInputReader _input;
    private PlayerStressHUD _hud;
    public PlayerStatusHUD StatusHUD => _hud != null ? _hud.StatusHUD : null;
    private PlayerCrosshair _crosshair;
    private PlayerInteractor _interactor;

    private void Awake()
    {
        _identity = GetComponent<NetworkIdentity>();
        _input = GetComponent<PlayerInputReader>();
        _interactor = GetComponent<PlayerInteractor>();
        if (_stress == null) _stress = GetComponent<PlayerStressController>();
        if (_health == null) _health = GetComponent<PlayerHealth>();
    }

    private void LateUpdate()
    {
        bool local = (!NetworkClient.active && !NetworkServer.active) ||
                     (_identity != null && _identity.isLocalPlayer);
        if (local && _notebookUI == null && _notebookPrefab != null)
        {
            NotebookPanel existing = null;
            foreach (var candidate in FindObjectsByType<NotebookPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var binding = candidate.GetComponentInParent<PlayerNotebookUI>();
                if (candidate.transform.root.gameObject.activeInHierarchy && (binding == null || !binding.IsBound)) { existing = candidate; break; }
            }
            if (existing != null)
            {
                _notebookUI = existing.GetComponentInParent<PlayerNotebookUI>();
                if (_notebookUI == null) _notebookUI = existing.GetComponentInParent<Canvas>().gameObject.AddComponent<PlayerNotebookUI>();
                _notebookUI.SetPanel(existing);
            }
            else { _notebookUI = Instantiate(_notebookPrefab).GetComponent<PlayerNotebookUI>(); _ownsNotebook = true; }
            _notebookUI.Bind(this);
        }
        if (!local && _notebookUI != null) ReleaseNotebook();
        UpdateStressHUD(local);
        UpdateCrosshair(local);
    }

    private void UpdateStressHUD(bool local)
    {
        // HUD stays visible through input blocking and death, as before.
        bool show = local && _stress != null;
        if (show && _hud == null && _hudPrefab != null)
        {
            _hud = Instantiate(_hudPrefab);
            _hud.name = "Local Stress HUD";
            _hud.Bind(_stress, _health);
        }
        if (_hud != null && _hud.gameObject.activeSelf != show) _hud.gameObject.SetActive(show);
    }

    private void UpdateCrosshair(bool local)
    {
        bool show = local && PlayerActionGuard.CanAct(gameObject) &&
                    _input != null && _input.isActiveAndEnabled && !_input.GameplayInputBlocked;
        if (show && _crosshair == null && _crosshairPrefab != null) _crosshair = Instantiate(_crosshairPrefab);
        if (_crosshair != null && _crosshair.gameObject.activeSelf != show) _crosshair.gameObject.SetActive(show);
        if (show && _crosshair != null) _crosshair.SetHighlighted(_interactor != null && _interactor.CanInteract && _interactor.CurrentTarget != null && _interactor.CurrentTarget.CanInteract(gameObject));
    }

    private void ReleaseNotebook()
    {
        if (_notebookUI == null) return;
        _notebookUI.Unbind();
        if (_ownsNotebook) { _notebookUI.gameObject.SetActive(false); Destroy(_notebookUI.gameObject); } 
        _notebookUI = null; _ownsNotebook = false;
    }

    private void OnDisable()
    {
        ReleaseNotebook();
        if (_hud != null) { _hud.gameObject.SetActive(false); Destroy(_hud.gameObject); }
        if (_crosshair != null) { _crosshair.gameObject.SetActive(false); Destroy(_crosshair.gameObject); }
        _hud = null;
        _crosshair = null;
    }
}
