using System.Collections;
using UnityEngine;
using Mirror;

// [로컬 클라] 디펜스 성공 시 봉인 항아리를 비추는 짧은 연출. 깃발 오브젝트에 붙임
// 서버(NetRitualFlow)가 깃발 근처의 살아 있는 플레이어에게만 TargetRpc로 Play를 부름
// 항아리 자리(_jarPoints)는 서버가 항아리를 생성할 위치로도 사용
public class NetSealCinematic : MonoBehaviour
{
    [Header("항아리 자리 (퇴마 순서대로, 최대 4개)")]
    [SerializeField] private Transform[] _jarPoints;

    [Header("카메라")]
    [Tooltip("연출 카메라 시작 위치")]
    [SerializeField] private Transform _camPoint;
    [Tooltip("비워 두면 플레이어 카메라 설정을 복사해서 만듦")]
    [SerializeField] private Camera _camera;
    [SerializeField, Min(0.5f)] private float _duration = 3f;
    [Tooltip("연출 동안 항아리 쪽으로 다가가는 거리")]
    [SerializeField, Min(0f)] private float _pushIn = 1.2f;
    [Tooltip("항아리 중심보다 살짝 위를 보도록")]
    [SerializeField] private float _lookHeight = 0.3f;

    public float Duration => _duration;
    public int JarPointCount => _jarPoints != null ? _jarPoints.Length : 0;
    public static bool IsPlaying { get; private set; }

    // [서버] 항아리 생성 위치. 칸이 모자라면 마지막 칸에 겹침
    public Transform GetJarPoint(int index)
    {
        if (JarPointCount == 0) return null;
        return _jarPoints[Mathf.Clamp(index, 0, _jarPoints.Length - 1)];
    }

    // [로컬 클라]
    public void Play(int jarIndex)
    {
        if (IsPlaying || _camPoint == null) return;

        Transform jar = GetJarPoint(jarIndex);
        if (jar == null) return;

        StartCoroutine(PlayRoutine(jar));
    }

    private IEnumerator PlayRoutine(Transform jar)
    {
        IsPlaying = true;

        PlayerInputReader input = null;
        PlayerCameraController look = null;
        NetworkIdentity local = NetworkClient.localPlayer;
        if (local != null)
        {
            local.TryGetComponent(out input);
            local.TryGetComponent(out look);
        }

        // 수첩 등으로 이미 막혀 있었다면 끝날 때 풀지 않음
        bool wasBlocked = input != null && input.GameplayInputBlocked;
        bool couldLook = look == null || look.CanLook;
        if (input != null) input.SetGameplayInputBlocked(true);
        if (look != null) look.CanLook = false;

        Camera cam = PrepareCamera();
        Vector3 target = jar.position + Vector3.up * _lookHeight;
        Vector3 start = _camPoint.position;
        Vector3 end = Vector3.MoveTowards(start, target, _pushIn);

        for (float t = 0f; t < _duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / _duration);
            cam.transform.position = Vector3.Lerp(start, end, k);
            cam.transform.LookAt(target);
            yield return null;
        }

        cam.enabled = false;

        // 마지막 퇴마면 결과 화면(EndingUI)이 이어서 입력을 잡고 있으므로 건드리지 않음
        GameManager gm = GameManager.Existing;
        bool gameOver = gm != null && gm.IsGameOver;
        if (!gameOver)
        {
            if (input != null && !wasBlocked) input.SetGameplayInputBlocked(false);
            if (look != null) look.CanLook = couldLook;
        }

        IsPlaying = false;
    }

    private Camera PrepareCamera()
    {
        Camera main = Camera.main;

        if (_camera == null)
        {
            var go = new GameObject("SealCinematicCamera");
            go.transform.SetParent(transform, false);
            _camera = go.AddComponent<Camera>();
            if (main != null) _camera.CopyFrom(main);
        }

        // 플레이어 카메라보다 나중에 그려서 화면을 덮음 (플레이어 카메라는 그대로 둠)
        _camera.depth = (main != null && main != _camera ? main.depth : 0f) + 10f;
        _camera.transform.SetPositionAndRotation(_camPoint.position, _camPoint.rotation);
        _camera.enabled = true;
        return _camera;
    }

    private void OnDisable()
    {
        // 씬 종료 등으로 중간에 끊긴 경우
        if (_camera != null) _camera.enabled = false;
        IsPlaying = false;
    }
}
