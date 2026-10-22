using Mirror;
using UnityEngine;

namespace ProjectShaman.AI
{
    // The camera this client is looking through. Each NetPlayer carries its own camera (all named/tagged like the scene's
    // Main Camera, same depth), so Camera.main or "highest depth" can land on the wrong one. Use the local player's camera,
    // and when it is switched off (spectating) the screen camera that belongs to no player.
    public static class AI_ViewerCamera
    {
        private static Camera[] _buffer = new Camera[8];
        private static Camera _cached;
        private static int _cachedFrame = -1;

        public static Camera Current
        {
            get
            {
                if (_cachedFrame == Time.frameCount)
                {
                    return _cached;
                }

                _cachedFrame = Time.frameCount;
                _cached = Find();
                return _cached;
            }
        }

        // Turn a world-space label so it reads flat to the viewer
        public static void Face(Transform label)
        {
            Camera viewer = Current;
            if (viewer != null)
            {
                label.rotation = viewer.transform.rotation;
            }
        }

        private static Camera Find()
        {
            // 1. The local player's own camera
            if (NetworkClient.localPlayer != null)
            {
                foreach (Camera own in NetworkClient.localPlayer.GetComponentsInChildren<Camera>())
                {
                    if (IsScreenCamera(own))
                    {
                        return own;
                    }
                }
            }

            // 2. Its camera is off (spectating): any screen camera outside a player, the scene's leftover MainCamera last
            if (Camera.allCamerasCount > _buffer.Length)
            {
                _buffer = new Camera[Camera.allCamerasCount];
            }

            int count = Camera.GetAllCameras(_buffer);
            Camera best = null;
            int bestRank = int.MinValue;

            for (int i = 0; i < count; i++)
            {
                Camera candidate = _buffer[i];
                if (!IsScreenCamera(candidate) || candidate.GetComponentInParent<NetworkIdentity>() != null)
                {
                    continue;
                }

                int rank = candidate.CompareTag("MainCamera") ? 0 : 1;
                if (best == null || rank > bestRank || (rank == bestRank && candidate.depth > best.depth))
                {
                    best = candidate;
                    bestRank = rank;
                }
            }

            return best != null ? best : Camera.main;
        }

        private static bool IsScreenCamera(Camera camera)
        {
            return camera != null && camera.isActiveAndEnabled && camera.targetTexture == null;
        }
    }
}
