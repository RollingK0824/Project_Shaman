using Mirror;
using UnityEngine;
using ProjectShaman.AI.Core;

namespace ProjectShaman.Network.AI
{
    // Quest list replication. Rides on the AI factory's scene object so no extra networked object is needed:
    // the server republishes QuestManager's list on every quest event, clients rebuild their read-only copy.
    public partial class Net_AIFactory
    {
        private readonly SyncList<QuestSyncEntry> _questSync = new SyncList<QuestSyncEntry>();

        private QuestManager _syncedQuests;
        private bool _questSyncDirty;

        private void ServerBindQuests()
        {
            _syncedQuests = QuestManager.Existing != null ? QuestManager.Existing : FindFirstObjectByType<QuestManager>();
            if (_syncedQuests == null)
            {
                AILog.Warn(AILog.NET, "QuestManager not in scene, quests are not replicated");
                return;
            }

            _syncedQuests.QuestOffered += HandleQuestChanged;
            _syncedQuests.QuestStarted += HandleQuestChanged;
            _syncedQuests.QuestUpdated += HandleQuestChanged;
            _syncedQuests.QuestEnded += HandleQuestChanged;
            PublishQuests();
        }

        private void ServerUnbindQuests()
        {
            if (_syncedQuests == null)
            {
                return;
            }

            _syncedQuests.QuestOffered -= HandleQuestChanged;
            _syncedQuests.QuestStarted -= HandleQuestChanged;
            _syncedQuests.QuestUpdated -= HandleQuestChanged;
            _syncedQuests.QuestEnded -= HandleQuestChanged;
            _syncedQuests = null;
        }

        private void HandleQuestChanged(IQuestView quest)
        {
            PublishQuests();
        }

        [Server]
        private void PublishQuests()
        {
            _questSync.Clear();
            _questSync.AddRange(_syncedQuests.ExportForNetwork());
        }

        private void ClientBindQuests()
        {
            if (isServer)
            {
                return;
            }

            _questSync.OnChange += HandleQuestSyncChanged;
            _questSyncDirty = true;
        }

        private void ClientUnbindQuests()
        {
            _questSync.OnChange -= HandleQuestSyncChanged;
        }

        private void HandleQuestSyncChanged(SyncList<QuestSyncEntry>.Operation op, int index, QuestSyncEntry item)
        {
            _questSyncDirty = true;
        }

        // A republish arrives as a clear plus adds; apply once per frame
        [ClientCallback]
        private void LateUpdate()
        {
            if (!_questSyncDirty || isServer)
            {
                return;
            }

            QuestManager quests = QuestManager.Existing;
            if (quests == null)
            {
                return;
            }

            _questSyncDirty = false;
            quests.ApplyNetworkQuests(_questSync);
        }
    }
}
