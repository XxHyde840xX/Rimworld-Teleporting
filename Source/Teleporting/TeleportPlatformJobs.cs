using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace alaestor_teleporting
{
	public abstract class JobDriver_UseTeleportPlatform_Generic : JobDriver
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed)
			=> this.pawn.Reserve(this.job.targetA, this.job, errorOnFailed: errorOnFailed);

		/*
		protected JobCondition IsToilDone()
		{
			if (this.job.targetA != null
				&& this.job.targetA.IsValid
				&& this.job.targetA.Thing != null
				&& this.job.targetA.Thing is Building_TeleportPlatform platform
				&& console.IsDoneTargeting()) // is this enough checks?
			{
				Logger.DebugVerbose("useTeleporterToil finished");
				return JobCondition.Succeeded;
			}
			else
			{
				return JobCondition.Ongoing;
			}
		}
		*/
	}

	public class JobDriver_UseTeleportPlatform_TeleportToLink : JobDriver_UseTeleportPlatform_Generic
	{
		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedOrNull<JobDriver_UseTeleportPlatform_TeleportToLink>(TargetIndex.A);
			this.FailOnBurningImmobile<JobDriver_UseTeleportPlatform_TeleportToLink>(TargetIndex.A);
			yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.InteractionCell).FailOn((Func<Toil, bool>)
				(to => !((Building_TeleportPlatform)to.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing).CanUseNow));

			Toil useTeleporterToil = new Toil();
			useTeleporterToil.defaultCompleteMode = ToilCompleteMode.PatherArrival; //ToilCompleteMode.Never;
			useTeleporterToil.initAction = (Action)(() =>
			{
				Pawn actor = useTeleporterToil.actor;
				Building_TeleportPlatform platform = (Building_TeleportPlatform)actor.jobs.curJob.GetTarget(TargetIndex.A).Thing;
				if (!platform.CanUseNow)
					return;

				platform.TryStartTeleport(actor);
				Logger.DebugVerbose("Pawn " + actor.Label + " began JobDriver_UseTeleportPlatform_TeleportToLink at ThindID " + platform.ThingID.ToString());
			});
			//useTeleporterToil.AddEndCondition(IsToilDone);
			yield return useTeleporterToil;
		}
	}
}// namespace alaestor_teleporting