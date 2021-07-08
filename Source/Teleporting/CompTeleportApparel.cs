﻿using RimWorld;
using System;
using System.Collections.Generic;
using Verse;


/*
 * IDEA
 * generic teleport comp
 * configure options via properties (wearable, OnlyTeleportSelf, etc)
 */

namespace alaestor_teleporting
{
	public class CompTeleportApparel : ThingComp
	{
		public CompProperties_TeleportApparel Props => (CompProperties_TeleportApparel)this.props;

		public Pawn Wearer
		{
			get
			{
				if (parent is Apparel apparel)
				{
					return apparel.Wearer;
				}
				else
				{
					Logger.Error("CompTeleportApparel::Wearer: isn't apparel");
					return null;
				}
			}
		}

		// Cooldown
		private CompCooldown CooldownComp => parent.GetComp<CompCooldown>() ?? null;
		private bool HasCooldownComp => CooldownComp != null;
		public bool UseCooldown => Props.useCooldown && TeleportingMod.settings.enableCooldown && TeleportingMod.settings.enableCooldown_ApparelComp;

		// NameLinkable
		private CompNameLinkable NameLinkableComp => parent.GetComp<CompNameLinkable>() ?? null;
		private bool HasNameLinkableComp => NameLinkableComp != null;
		public bool UseNameLinkable => Props.useNameLinkable;

		// Refuelable consumable
		private CompRefuelable RefuelableComp => parent.GetComp<CompRefuelable>() ?? null;
		private bool HasRefuelableComp => RefuelableComp != null;
		public bool UseRefuelable => TeleportingMod.settings.enableFuel && Props.useRefuelable; // && settings.enableConsumableApparel
		public int RemainingFuel => (int)RefuelableComp.Fuel; // calling this when HasRefuelableComp is false will error

		// Teleport settings
		public bool CanDoTeleport_ShortRange => Props.shortRange;
		public bool CanDoTeleport_LongRange => Props.longRange;
		public bool CanTeleportOthers => Props.canTeleportOthers;

		private void HandleFuel(
			bool cheat = false,
			bool shortRange_Teleport = false,
			bool longRange_Teleport = false,
			bool nameLink_Teleport = false)
		{
			if (!cheat)
			{
				if (UseRefuelable)
				{
					if (HasRefuelableComp)
					{
						
					}
					else Logger.Error("CompTeleportApparel::HandleFuel: UseRefuelable is true but RefuelableComp is null");
				}
			}
		}

		private void AfterSuccessfulTeleport(bool cheat = false, int setCooldown = 0, int consumeFuel = 0)
		{
			if (!cheat)
			{
				if (UseCooldown)
				{
					if (HasCooldownComp)
					{
						CooldownComp.SetSeconds(setCooldown);
					}
					else Logger.Error("CompTeleportApparel::HandleCooldown: UseCooldown is true but CooldownComp is null");
				}

				if (UseRefuelable)
				{
					if (HasRefuelableComp)
					{
						RefuelableComp.ConsumeFuel(consumeFuel);
					}
					else Logger.Error("CompTeleportApparel::HandleCooldown: UseRefuelable is true but RefuelableComp is null");
				}
			}
		}

		private void AfterSuccessfulTeleport_Link(bool cheat = false)
		{
			AfterSuccessfulTeleport(
				cheat: cheat,
				setCooldown: TeleportingMod.settings.nameLinkable_CooldownDuration,
				consumeFuel: 0
			);
		}

		private void AfterSuccessfulTeleport_Normal(TeleportData teleportData)
		{
			if (teleportData.longRangeFlag)
			{
				AfterSuccessfulTeleport(
					cheat: teleportData.cheat,
					setCooldown: TeleportingMod.settings.longRange_CooldownDuration,
					consumeFuel: TeleportBehavior.FuelCostToTravel(true, teleportData.distance)
				);
			}
			else
			{
				AfterSuccessfulTeleport(
					cheat: teleportData.cheat,
					setCooldown:  TeleportingMod.settings.shortRange_CooldownDuration,
					consumeFuel: TeleportBehavior.FuelCostToTravel(false, teleportData.distance)
				);
			}
		}

		public void StartTeleport_ShortRange(bool cheat = false)
		{
			if (CanDoTeleport_ShortRange)
			{
				if (Wearer != null && Wearer.Map != null)
				{
					if (CanTeleportOthers)
					{
						TeleportBehavior.StartTeleportTargetting(false, Wearer, AfterSuccessfulTeleport_Normal, cheat: cheat);
					}
					else
					{
						TeleportBehavior.StartTeleportPawn(false, Wearer, AfterSuccessfulTeleport_Normal, cheat: cheat);
					}
				}
				else Logger.Error("CompTeleportApparel::StartTeleport_ShortRange: invalid wearer");
			}
			else Logger.Error("CompTeleportApparel::StartTeleport_ShortRange: disallowed, CanDoShortRangeTelePort is false");
		}

		public void StartTeleport_LongRange(bool cheat = false)
		{
			if (CanDoTeleport_LongRange)
			{
				if (Wearer != null && Wearer.Map != null)
				{
					Logger.DebugVerbose("CompTeleportApparel::StartTeleport_LongRange: called",
						"Wearer: " + Wearer.Label ?? "(no label)"
					);

					int fuel = (UseRefuelable && HasRefuelableComp) ? (int)RefuelableComp.Fuel : TeleportingMod.settings.longRange_FuelCost;

					if (CanTeleportOthers)
					{
						TeleportBehavior.StartTeleportTargetting(true, Wearer, AfterSuccessfulTeleport_Normal, fuel, cheat);
					}
					else
					{
						TeleportBehavior.StartTeleportPawn(true, Wearer, AfterSuccessfulTeleport_Normal, fuel, cheat);
					}
				}
				else Logger.Error("CompTeleportApparel::StartTeleport_LongRange: invalid wearer");
			}
			else Logger.Error("CompTeleportApparel::StartTeleport_LongRange: disallowed, CanDoLongRangeTelePort is false");
		}

		public void StartTeleport_LinkedThing(bool cheat = false)
		{
			if (UseNameLinkable)
			{
				if (HasNameLinkableComp)
				{
					CompNameLinkable nameLinkable = NameLinkableComp;
					if (nameLinkable.IsLinkedToSomething)
					{
						if (nameLinkable.HasValidLinkedThing)
						{
							Thing destination = nameLinkable.LinkedThing;
							if (destination.Map != null && destination.InteractionCell.IsValid)
							{
								if (CanTeleportOthers)
								{
									TeleportTargeter.StartChoosingLocal(
										Wearer,
										DoTeleport,
										TeleportBehavior.targetTeleportSubjects,
										mouseAttachment: TeleportBehavior.localTeleportMouseAttachment);
								}
								else
								{
									DoTeleport(Wearer);
								}

								void DoTeleport(LocalTargetInfo target)
								{
									if (target.IsValid && target.HasThing && target.Thing is Pawn pawn)
									{
										if (TeleportBehavior.ExecuteTeleport(pawn, destination.Map, destination.InteractionCell))
										{
											Logger.Debug(
												"CompTeleportApparel::StartTeleport_LinkedThing::DoTeleport: Teleported "
													+ pawn.Label
													+ " from \"" + nameLinkable.Name
													+ "\" to \"" + nameLinkable.GetNameOfLinkedLinkedThing + "\"",
												"Destination Map: " + destination.Map.ToString(),
												"Destination Cell: " + destination.InteractionCell.ToString()
											);
											AfterSuccessfulTeleport_Link(cheat: cheat);
										}
										else Logger.Error("CompTeleportApparel::StartTeleport_LinkedThing::DoTeleport: ExecuteTeleport failed.");
									}
									else
									{
										Logger.Error(
											"CompTeleportApparel::StartTeleport_LinkedThing::DoTeleport: invalid target",
											"valid: " + target.IsValid.ToString(),
											"thing: " + (target.HasThing ? (target.Thing.Label) : "None" )
										);
									}
								}
							}
							else
							{
								Logger.Error(
									"CompTeleportApparel::StartTeleport_LinkedThing: destination map or cell was invalid!",
									"Map: " + destination.Map.ToString(),
									"Cell: " + destination.InteractionCell.ToString()
								);
							}

						}
						else Logger.Error("CompTeleportApparel::StartTeleport_LinkedThing: nameLinkable is linked to invalid thing");
					}
					else Logger.Error("CompTeleportApparel::StartTeleport_LinkedThing: nameLinkable isn't linked");
				}
				else Logger.Error("CompTeleportApparel::StartTeleport_LinkedThing: UseNameLinkable is true but NameLinkable is null");
			}
			else Logger.Error("CompTeleportApparel::StartTeleport_LinkedThing: UseNameLinkable is false");
		}

		/*
		public void SelfDestruct()
		{
			Logger.DebugVerbose(parent.Label + " self destructed");
			this.parent.SplitOff(1).Destroy();
		}
		*/

		public override void Initialize(CompProperties props)
		{
			base.Initialize(props);
			if (UseCooldown && !HasCooldownComp)
			{
				Logger.Error(
					"CompTeleportApparel set to use CompCooldown but has no CompCooldown",
					"Parent: " + parent.Label
				);
			}

			if (UseNameLinkable && !HasNameLinkableComp)
			{
				Logger.Error(
					"CompTeleportApparel set to use CompNameLinkable but has no CompNameLinkable",
					"Parent: " + parent.Label
				);
			}

			if (UseRefuelable && !HasRefuelableComp)
			{
				Logger.Error(
					"CompTeleportApparel set to use CompRefuelable but has no CompRefuelable",
					"Parent: " + parent.Label
				);
			}
		}

		public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
		{
			foreach (Gizmo gizmo in base.CompGetWornGizmosExtra())
				yield return gizmo;

			if (Find.Selector.SingleSelectedThing == Wearer)
			{
				// common comps
				bool isOnCooldown = false;
				string cooldownRemainingString = null;
				if (UseCooldown && HasCooldownComp && CooldownComp.IsOnCooldown)
				{
					isOnCooldown = true;
					cooldownRemainingString = string.Format(
						"On cooldown for {0} more second(s)", // TODO translated version
						CooldownComp.SecondsRemaining);
				}

				string fuelRemainingDesc = null;
				if (UseRefuelable && HasRefuelableComp)
				{
					fuelRemainingDesc = string.Format("{0} fuel remaining", (int)RefuelableComp.Fuel);
				}


				if (CanDoTeleport_ShortRange)
				{
					yield return GizmoHelper.MakeCommandAction(
						"TeleportApparel_ShortRange",
						delegate
						{
							Logger.Debug("CompTeleportApparel: called Gizmo: Short Range Teleport");
							StartTeleport_ShortRange();
						},
						disabled: isOnCooldown,
						disabledReason: cooldownRemainingString,
						description: fuelRemainingDesc
					);
				}

				if (CanDoTeleport_LongRange)
				{
					yield return GizmoHelper.MakeCommandAction(
						"TeleportApparel_LongRange",
						delegate
						{
							Logger.Debug("CompTeleportApparel: called Gizmo: Long Range Teleport");
							StartTeleport_LongRange();
						},
						disabled: isOnCooldown,
						disabledReason: cooldownRemainingString,
						description: fuelRemainingDesc
					);
				}

				if (UseNameLinkable)
				{
					if (HasNameLinkableComp)
					{
						var nameLinkable = NameLinkableComp;
						if (nameLinkable.IsLinkedToSomething)
						{
							if (nameLinkable.HasValidLinkedThing)
							{
								yield return GizmoHelper.MakeCommandAction(
									"TeleportApparel_TeleportToLink",
									delegate
									{
										Logger.Debug("CompTeleportApparel: called Gizmo: Teleport to Link");
										StartTeleport_LinkedThing();
									},
									disabled: isOnCooldown,
									disabledReason: cooldownRemainingString
								);
							}
							else
							{
								yield return GizmoHelper.MakeCommandAction(
									"TeleportApparel_TeleportToLink",
									disabled: true,
									disabledReason: "Teleporting_CompNameLinkable_NotLinked".Translate()
								);
							}


							yield return GizmoHelper.MakeCommandAction(
								"TeleportApparel_Unlink",
								delegate
								{
									Logger.Debug("CompTeleportApparel: called Gizmo: Unlink");
									RefuelableComp.ConsumeFuel(1);
									nameLinkable.Unlink();
								},
								description: fuelRemainingDesc
							);
						}
						else
						{
							yield return GizmoHelper.MakeCommandAction(
								"TeleportApparel_MakeLink",
								delegate
								{
									Logger.Debug("CompTeleportApparel: called Gizmo: Make Link");
									nameLinkable.BeginMakeLink();
								}
							);
						}
					}
					else
					{
						Logger.Error("CompTeleportApparel: UseNameLinkable is true but NameLinkable is null",
							"Parent: " + parent.Label
						);
					}
				}

				/*
				if (DebugSettings.godMode)
				{
					yield return new Command_Action
					{
						defaultLabel = "Cheat_ShortTeleDebugGizmo_Label".Translate(), //"Tele Local",
						defaultDesc = "Cheat_ShortTeleDebugGizmo_Desc".Translate(), //"Teleport on map layer",
						activateSound = SoundDef.Named("Click"),
						action = delegate
						{
							Logger.Debug("TeleportBelt_Local:: called Godmode Gizmo: Short Range Teleport");
							TeleportBehavior.StartTeleportTargetting(false, Wearer, cheat: true);
						}
					};

					yield return new Command_Action
					{
						defaultLabel = "Cheat_LongTeleDebugGizmo_Label".Translate(),
						defaultDesc = "Cheat_LongTeleDebugGizmo_Desc".Translate(),
						activateSound = SoundDef.Named("Click"),
						action = delegate
						{
							Logger.Debug("TeleportBelt_Local:: called Godmode Gizmo: Long Range Teleport");
							TeleportBehavior.StartTeleportTargetting(true, Wearer, cheat: true);
						}
					};
				}
				*/
			}
		}
	}

	public class CompProperties_TeleportApparel : CompProperties
	{
		public bool shortRange = false;
		public bool longRange = false;
		public bool useNameLinkable = false;
		public bool useCooldown = false;
		public bool useRefuelable = false;
		public bool canTeleportOthers = false;

		public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
		{
			foreach (string configError in base.ConfigErrors(parentDef))
				yield return configError;

			if (!(shortRange || longRange || useNameLinkable))
			{
				yield return "All teleport types are false. At least one should be true: shortRange, longRange, useNameLinkable";
			}
		}

		public CompProperties_TeleportApparel()
		{
			this.compClass = typeof(CompTeleportApparel);
		}

		public CompProperties_TeleportApparel(Type compClass) : base(compClass)
		{
			this.compClass = compClass;
		}
	}
}// namespace alaestor_teleporting
