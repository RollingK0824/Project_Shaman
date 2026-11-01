using UnityEngine;
using Mirror;


// [클라] 제작 요청을 서버로 전달, [서버] 레시피를 다시 검사하고 제작 확정
// 레시피는 PlayerLocalUIBootstrap 레시피 목록의 인덱스로 전달 (같은 NetPlayer 프리팹이라 서버, 클라 목록 같음)
// 재료 차감과 결과물 지급은 NetSharedMaterials, NetPlayerInventory에서 자동으로 동기화
public class NetPlayerCrafting : NetworkBehaviour
{
    private PlayerCraftingController _crafting;
    private PlayerLocalUIBootstrap _bootstrap;
    private NetPlayerStatus _status;

    private void Awake()
    {
        _crafting = GetComponent<PlayerCraftingController>();
        _bootstrap = GetComponent<PlayerLocalUIBootstrap>();
        _status = GetComponent<NetPlayerStatus>();

    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _crafting.SetAuthorityCheck(ServerCanCraft);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        _crafting.CraftRequested += HandleCraftRequested;
    }

    public override void OnStopServer()
    {
        _crafting.SetAuthorityCheck(null);

        base.OnStopServer();
    }

    public override void OnStopLocalPlayer()
    {
        _crafting.CraftRequested -= HandleCraftRequested;
        base.OnStopLocalPlayer();
    }

    //[서버] 게임 진행 중이고, 살아 있을 때만 제작 허용 (줍기 판정과 같은 조건)
    private bool ServerCanCraft()
    {
        if (!NetworkServer.active) return false;
        if (_status != null && _status.IsDead) return false;

        GameManager gm = GameManager.Instance;
        return gm != null && gm.CurrentGameState == GameState.Ongoing;
    }


    // [로컬 클라] 제작 UI 요청을 레시피 인덱스로 바꿔 서버에 전달
    // 호스트는 CanExecuteLocally가 true라서 이 이벤트 없이 바로 TryCraft
    private void HandleCraftRequested(CraftingRecipeData recipe)
    {
        int index = IndexOfRecipe(recipe);

        if (index < 0)
        {
            Debug.LogWarning($"[NetPlayerCrafting] 레시피 목록에 없는 레시피: {recipe?.DisplayName}", this);
            _crafting.NotifyCraftResult(null, false);
            return;
        }

        CmdRequestCraft(index);
    }

    // [서버] 클라가 보낸 인덱스로 서버 쪽 목록에서 레시피 꺼내서 다시 판정
    [Command]
    private void CmdRequestCraft(int recipeIndex)
    {
        CraftingRecipeData recipe = _bootstrap != null && recipeIndex >= 0 && recipeIndex < _bootstrap.CraftingRecipes.Count
            ? _bootstrap.CraftingRecipes[recipeIndex] : null;
        bool success = recipe != null && _crafting.TryCraft(recipe);
        TargetCraftResult(recipeIndex, success);
        Debug.Log($"[NetPlayerCrafting] 제작 판정 : {name} -> {recipe?.DisplayName} {(success ? "성공" : "거절")}", this);
    }

    [TargetRpc]
    private void TargetCraftResult(int recipeIndex, bool success)
    {
        var recipe = _bootstrap != null && recipeIndex >= 0 && recipeIndex < _bootstrap.CraftingRecipes.Count
            ? _bootstrap.CraftingRecipes[recipeIndex] : null;
        _crafting.NotifyCraftResult(recipe, success);
    }

    private int IndexOfRecipe(CraftingRecipeData recipe)
    {
        if (_bootstrap == null || recipe == null) return -1;

        for (int i = 0; i < _bootstrap.CraftingRecipes.Count; i++)
        {
            if (_bootstrap.CraftingRecipes[i] ==recipe) return i;
        }

        return -1;
    }
}
