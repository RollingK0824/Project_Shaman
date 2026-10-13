using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    public static class GhostVerbExecutor
    {
        public static bool TryApply(IGhostTarget target, in GhostActionContext context)
        {
            switch (context.Verb)
            {
                case GhostActionVerb.Attack:
                    return TryAttack(target, context);

                case GhostActionVerb.SleepParalysis when target is ISleepParalyzable paralyzable:
                    paralyzable.ReceiveSleepParalysis(context);
                    return true;

                case GhostActionVerb.Defile when target is IGhostDefilable defilable:
                    defilable.ReceiveDefile(context);
                    AddAreaStress(target, context, "defiled");
                    return true;

                case GhostActionVerb.Absorb when target is IGhostAbsorbable absorbable:
                    absorbable.ReceiveAbsorb(context);
                    AddAreaStress(target, context, "absorbed");
                    return true;

                case GhostActionVerb.Haunt when target is IGhostHauntable hauntable:
                    hauntable.ReceiveHaunt(context);
                    AddAreaStress(target, context, "haunted");
                    return true;

                default:
                    AILog.Warn(AILog.GHOST, $"{context.GhostId} cannot apply {context.Verb} to {target?.Kind}");
                    return false;
            }
        }

        private static bool TryAttack(IGhostTarget target, in GhostActionContext context)
        {
            IDamageable damageable = target.gameObject.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.IsDead)
            {
                AILog.Warn(AILog.GHOST, $"{context.GhostId} attack on {target.gameObject.name} skipped (no live IDamageable)");
                return false;
            }

            Vector3 position = target.gameObject.transform.position;
            float damage = context.Action != null ? context.Action.Damage : 0f;
            GameObject source = context.Entity != null ? context.Entity.gameObject : null;

            damageable.ReceiveDamage(damage, source);
            AILog.Log(AILog.GHOST, context.GhostId, $"Attack {target.Kind} {target.gameObject.name} damage={damage:F1} dead={damageable.IsDead}");

            if (damageable.IsDead)
            {
                AddAreaStress(position, target.Kind, context, "killed");
            }

            return true;
        }

        private static void AddAreaStress(IGhostTarget target, in GhostActionContext context, string what)
        {
            AddAreaStress(target.gameObject.transform.position, target.Kind, context, what);
        }

        private static void AddAreaStress(Vector3 position, GhostTargetKind kind, in GhostActionContext context, string what)
        {
            if (context.Ledger == null || context.Action == null)
            {
                return;
            }

            context.Ledger.AddStressAround(position, context.Ledger.Config.NightWitnessRadius, context.Action.VillagerStress, $"{context.ActionId}: {kind} {what} nearby");
        }
    }
}
