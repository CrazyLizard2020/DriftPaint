using System.Collections.Generic;
using DriftPaint.Cars;
using NUnit.Framework;
using UnityEngine;

namespace DriftPaint.Tests
{
    public class VehicleStabilityFSMTests
    {
        const float Dt = 0.02f;

        StabilitySettings _settings;
        VehicleStabilityFSM _fsm;

        [SetUp]
        public void SetUp()
        {
            _settings = new StabilitySettings();
            _fsm = new VehicleStabilityFSM();
        }

        #region Lecturas

        static StabilityInput Wheels(int count, float tilt = 0f, Vector3 velocity = default)
        {
            return new StabilityInput
            {
                Velocity = velocity,
                Tilt = tilt,
                SupportTilt = tilt,
                HasRecoverySpace = true,
                Ground = new GroundSample
                {
                    WheelContactCount = count,
                    Confidence = count / 4f,
                    HasValidWheelSupport = count > 0,
                    SupportNormal = Vector3.up,
                    Basis = count > 0 ? SupportBasis.From(Vector3.up, Vector3.forward) : default
                }
            };
        }

        static StabilityInput Air(float verticalSpeed, float prediction = -1f)
        {
            StabilityInput input = Wheels(0, 0f, new Vector3(0f, verticalSpeed, 10f));
            input.Ground.NormalSpeed = verticalSpeed;
            input.Ground.HasPrediction = prediction >= 0f;
            input.Ground.PredictedTime = Mathf.Max(0f, prediction);
            input.Ground.PredictedNormal = Vector3.up;
            return input;
        }

        static StabilityInput Body(float tilt, bool space = true, Vector3 velocity = default)
        {
            StabilityInput input = Wheels(0, tilt, velocity);
            input.Ground.HasBodyGroundContact = true;
            input.Ground.BodyContactNormal = Vector3.up;
            input.HasRecoverySpace = space;
            return input;
        }

        StabilityFrame Step(StabilityInput input) => _fsm.Evaluate(input, _settings, Dt);

        StabilityFrame Run(StabilityInput input, float seconds)
        {
            StabilityFrame frame = default;
            int steps = Mathf.CeilToInt(seconds / Dt - 1e-4f);
            for (int i = 0; i < steps; i++) frame = Step(input);
            return frame;
        }

        void ReachGrounded()
        {
            Run(Wheels(4), 0.4f);
            Assert.AreEqual(VehicleStabilityState.Grounded, _fsm.State);
        }

        #endregion

        #region Inicio y confirmación

        [Test]
        public void Starts_InLandingWithSupport_AndInAirWithout()
        {
            Step(Wheels(4));
            Assert.AreEqual(VehicleStabilityState.Landing, _fsm.State);

            _fsm.Reset();
            Step(Air(-1f));
            Assert.AreEqual(VehicleStabilityState.Airborne, _fsm.State);
        }

        [Test]
        public void Confirmation_NeedsContinuousSupport_ThenBlendsInsideLanding()
        {
            StabilityFrame frame = Step(Wheels(4));
            Assert.IsFalse(frame.CanDrive);
            frame = Step(Wheels(4));
            Assert.IsFalse(frame.CanDrive);
            frame = Step(Wheels(4));
            Assert.IsTrue(frame.CanDrive, "Tres pasos de 0,02 s completan 0,06 s.");
            Assert.AreEqual(VehicleStabilityState.Landing, frame.State);
            Assert.Less(frame.DriveBlend, 1f);

            float previous = frame.DriveBlend;
            for (int i = 0; i < 10 && _fsm.State == VehicleStabilityState.Landing; i++)
            {
                frame = Step(Wheels(4));
                Assert.GreaterOrEqual(frame.DriveBlend, previous);
                previous = frame.DriveBlend;
            }
            Assert.AreEqual(VehicleStabilityState.Grounded, _fsm.State);
            Assert.AreEqual(1f, frame.DriveBlend);
        }

        [Test]
        public void Confirmation_RestartsWhenContactFlickers()
        {
            for (int i = 0; i < 20; i++)
            {
                Step(Wheels(4));
                StabilityFrame frame = Step(Wheels(1));
                Assert.IsFalse(frame.CanDrive);
            }
            Assert.AreEqual(VehicleStabilityState.Landing, _fsm.State);
        }

        [TestCase(3, true)]
        [TestCase(2, false)]
        [TestCase(1, false)]
        public void ThreeWheels_Confirm(int wheels, bool drives)
        {
            StabilityFrame frame = Run(Wheels(wheels, 0f, Vector3.forward * 10f), 0.5f);
            Assert.AreEqual(drives, frame.CanDrive);
        }

        [Test]
        public void JustRegained_And_JustLost_HappenOnce()
        {
            int regained = 0, lost = 0;
            for (int i = 0; i < 20; i++) if (Step(Wheels(4)).JustRegainedSupport) regained++;
            for (int i = 0; i < 20; i++) if (Step(Air(3f)).JustLostSupport) lost++;
            Assert.AreEqual(1, regained);
            Assert.AreEqual(1, lost);
        }

        #endregion

        #region Pérdida de apoyo

        [Test]
        public void ZeroWheels_CutSameStep()
        {
            ReachGrounded();
            StabilityFrame frame = Step(Air(2f));
            Assert.AreEqual(VehicleStabilityState.Takeoff, frame.State);
            Assert.AreEqual(0f, frame.DriveBlend);
            Assert.IsFalse(frame.CanDrive || frame.CanApplyGrip || frame.CanApplySteering || frame.ApplySuspension);
            Assert.IsTrue(frame.JustLostSupport);
        }

        [Test]
        public void PartialContact_IsToleratedReduced_ThenReconfirmsWithoutNewEpisode()
        {
            ReachGrounded();
            int episode = Step(Wheels(4)).LandingEpisode;

            StabilityFrame frame = Step(Wheels(2));
            Assert.AreEqual(VehicleStabilityState.Grounded, frame.State);
            Assert.Greater(frame.DriveBlend, 0f);
            Assert.Less(frame.DriveBlend, 1f);

            frame = Run(Wheels(2), 0.1f);
            Assert.AreEqual(VehicleStabilityState.Landing, frame.State);
            Assert.AreEqual(0f, frame.DriveBlend);
            frame = Run(Wheels(4), 0.4f);
            Assert.AreEqual(VehicleStabilityState.Grounded, frame.State);
            Assert.AreEqual(episode, frame.LandingEpisode);
        }

        [Test]
        public void EdgeFall_GoesStraightToAir()
        {
            ReachGrounded();
            Assert.AreEqual(VehicleStabilityState.Airborne, Step(Air(-0.5f)).State);
        }

        [Test]
        public void FallWithPrediction_GoesStraightToPreLanding()
        {
            ReachGrounded();
            Assert.AreEqual(VehicleStabilityState.PreLanding, Step(Air(-2f, 0.1f)).State);
        }

        #endregion

        #region Vuelo y aterrizaje

        [Test]
        public void Takeoff_EndsByTimeOrByFalling()
        {
            ReachGrounded();
            Run(Air(5f), 0.06f);
            Assert.AreEqual(VehicleStabilityState.Takeoff, _fsm.State);
            Run(Air(5f), 0.2f);
            Assert.AreEqual(VehicleStabilityState.Airborne, _fsm.State);

            _fsm.Reset();
            ReachGrounded();
            Step(Air(1f));
            Assert.AreEqual(VehicleStabilityState.PreLanding, Step(Air(-1f, 0.2f)).State);
        }

        [Test]
        public void PreLanding_GoesBackWhenPredictionIsLost()
        {
            Step(Air(-2f));
            Assert.AreEqual(VehicleStabilityState.PreLanding, Step(Air(-2f, 0.2f)).State);
            Assert.AreEqual(VehicleStabilityState.Airborne, Step(Air(-2f)).State);
        }

        [Test]
        public void Prediction_NeverDrivesNorTouchesGround()
        {
            StabilityFrame frame = Run(Air(-2f, 0.05f), 0.5f);
            Assert.AreEqual(VehicleStabilityState.PreLanding, frame.State);
            Assert.IsFalse(frame.CanDrive || frame.ApplySuspension || frame.ApplyLandingAbsorption);
        }

        [Test]
        public void LandingEpisode_CountsGenuineFlightsOnly()
        {
            ReachGrounded();
            Run(Air(-2f), 0.3f);
            Assert.AreEqual(1, Step(Wheels(4)).LandingEpisode);
            float window = Step(Wheels(4)).LandingWindow;
            Assert.Greater(window, 0.9f);

            // Rebotes cortos: siguen siendo el mismo aterrizaje y la ventana no vuelve a empezar.
            for (int i = 0; i < 8; i++)
            {
                Run(Air(1f), 0.04f);
                Step(Wheels(4));
            }
            StabilityFrame frame = Step(Wheels(4));
            Assert.AreEqual(1, frame.LandingEpisode);
            Assert.AreEqual(0f, frame.LandingWindow);

            Run(Air(-2f), 0.3f);
            Assert.AreEqual(2, Step(Wheels(4)).LandingEpisode);
        }

        [Test]
        public void OneTransitionPerStep()
        {
            var random = new System.Random(7);
            for (int i = 0; i < 4000; i++)
            {
                int before = _fsm.Transitions;
                VehicleStabilityState state = _fsm.State;
                int pick = random.Next(6);
                Step(pick == 0 ? Air(3f) : pick == 1 ? Air(-3f) : pick == 2 ? Air(-3f, 0.1f) : pick == 3 ? Wheels(random.Next(1, 5)) : pick == 4 ? Body(random.Next(0, 180)) : Wheels(4));
                Assert.LessOrEqual(_fsm.Transitions - before, 1);
                Assert.AreEqual(_fsm.Transitions != before, _fsm.State != state);
            }
        }

        #endregion

        #region Permisos

        [Test]
        public void Permissions_FollowTheState()
        {
            ReachGrounded();
            StabilityFrame grounded = Step(Wheels(4));
            Assert.IsTrue(grounded.ApplySuspension && grounded.CanDrive && grounded.ApplySupportAlignment && grounded.ApplyAngularLimit);
            Assert.IsFalse(grounded.ApplyAirAlignment || grounded.ApplyLandingAssist || grounded.ApplyLandingAbsorption || grounded.ApplyRecovery || grounded.ApplyFlightGravity || grounded.ApplyAutoRoll);

            StabilityFrame air = Run(Air(-2f), 0.3f);
            Assert.IsTrue(air.ApplyAirAlignment && air.ApplyFlightGravity && air.ApplyAngularLimit);
            Assert.IsFalse(air.ApplySuspension || air.CanDrive || air.ApplySupportAlignment || air.ApplyLandingAssist || air.ApplyLandingAbsorption || air.ApplyAutoRoll || air.ApplyRecovery);

            StabilityFrame landing = Step(Wheels(4));
            Assert.IsTrue(landing.ApplySuspension && landing.ApplyLandingAssist && landing.ApplyLandingAbsorption && landing.ApplyAngularLimit);
            Assert.IsFalse(landing.ApplyAirAlignment || landing.ApplyRecovery || landing.ApplyFlightGravity || landing.ApplySupportAlignment);
        }

        [Test]
        public void TiltCorrections_AreExclusive()
        {
            var random = new System.Random(3);
            for (int i = 0; i < 3000; i++)
            {
                int pick = random.Next(4);
                StabilityFrame f = Step(pick == 0 ? Air(-3f) : pick == 1 ? Wheels(random.Next(1, 5), random.Next(0, 60)) : pick == 2 ? Body(random.Next(0, 180)) : Wheels(4));
                int active = (f.ApplyAirAlignment ? 1 : 0) + (f.ApplySupportAlignment ? 1 : 0) + (f.ApplyLandingAssist ? 1 : 0) + (f.ApplyAutoRoll ? 1 : 0) + (f.ApplyRecovery ? 1 : 0);
                Assert.LessOrEqual(active, 1);
                Assert.IsTrue(f.ApplyAngularLimit);
                if (f.DriveBlend > 0f) Assert.IsTrue(f.Basis.Valid);
            }
        }

        [Test]
        public void AutoRoll_OnlyWithPartialContactAndBelowItsAngle()
        {
            Assert.IsTrue(Step(Body(50f, true, Vector3.forward * 10f)).ApplyAutoRoll);
            _fsm.Reset();
            Assert.IsTrue(Step(Wheels(2, 30f, Vector3.forward * 10f)).ApplyAutoRoll);
            _fsm.Reset();
            Assert.IsFalse(Step(Wheels(4, 30f)).ApplyAutoRoll);
            _fsm.Reset();
            Assert.IsFalse(Step(Body(_settings.AutoRollMaxAngle + 1f)).ApplyAutoRoll);
            _fsm.Reset();
            Assert.IsFalse(Step(Air(-2f)).ApplyAutoRoll);
        }

        #endregion

        #region Recuperación

        [Test]
        public void Flip_StartsRecoveryAfterDetectTime_WithOneImpulse()
        {
            int impulses = 0;
            float elapsed = 0f;
            while (_fsm.State != VehicleStabilityState.SelfRecovery && elapsed < 5f)
            {
                if (Step(Body(170f)).RequestRecoveryImpulse) impulses++;
                elapsed += Dt;
            }
            Assert.AreEqual(_settings.FlipDetectTime, elapsed, 0.05f);
            for (int i = 0; i < 20; i++) if (Step(Body(170f)).RequestRecoveryImpulse) impulses++;
            Assert.AreEqual(1, impulses);
        }

        [Test]
        public void Recovery_SurvivesLosingContactByItsOwnImpulse_AndEndsInLanding()
        {
            Run(Body(170f), _settings.FlipDetectTime + Dt);
            Assert.AreEqual(VehicleStabilityState.SelfRecovery, _fsm.State);
            StabilityInput spinning = Air(2f);
            spinning.Tilt = 120f;
            StabilityFrame frame = Run(spinning, 0.4f);
            Assert.AreEqual(VehicleStabilityState.SelfRecovery, frame.State);
            Assert.IsTrue(frame.ApplyRecovery && frame.RecoveryWarning);

            frame = Step(Wheels(4, 10f));
            Assert.AreEqual(VehicleStabilityState.Landing, frame.State);
            Assert.IsFalse(frame.RequestRespawn);
        }

        [Test]
        public void Recovery_EndsInTheAirOnceUpright_AndDoesNotTimeOut()
        {
            Run(Body(170f), _settings.FlipDetectTime + Dt);
            StabilityFrame frame = Step(Air(-1f, 0.2f));
            Assert.AreEqual(VehicleStabilityState.PreLanding, frame.State);
            frame = Run(Air(-1f, 0.2f), _settings.SelfRecoveryMaxTime + 0.5f);
            Assert.IsFalse(frame.RequestRespawn || frame.RecoveryWarning);
        }

        [Test]
        public void Flip_KeepsCountingWhenTheRoofFlickers()
        {
            StabilityInput bounce = Air(-0.5f);
            bounce.Tilt = 180f;
            bool attempted = false;
            for (float t = 0f; t < _settings.FlipDetectTime + 0.3f && !attempted; t += 0.42f)
            {
                attempted |= Run(Body(180f), 0.4f).State == VehicleStabilityState.SelfRecovery;
                if (!attempted) Step(bounce);
            }
            Assert.IsTrue(attempted);
        }

        [Test]
        public void Recovery_Timeout_RequestsRespawnOnce()
        {
            Run(Body(170f), _settings.FlipDetectTime + Dt);
            int requests = 0;
            for (float t = 0f; t < _settings.SelfRecoveryMaxTime + 0.1f; t += Dt)
            {
                StabilityFrame frame = Step(Body(170f));
                if (!frame.RequestRespawn) continue;
                requests++;
                Assert.AreEqual(RespawnReason.RecoveryTimeout, frame.RespawnReason);
                // Quien la pide reinicia el estado al reubicar.
                _fsm.Reset();
                break;
            }
            Assert.AreEqual(1, requests);
        }

        [Test]
        public void NoSpace_BlinksAndThenRequestsRespawn()
        {
            StabilityFrame frame = Run(Body(170f, space: false), _settings.FlipDetectTime + Dt);
            Assert.IsTrue(frame.RecoveryWarning);
            Assert.AreNotEqual(VehicleStabilityState.SelfRecovery, frame.State);

            bool requested = false;
            for (float t = 0f; t < _settings.FlipBlinkTime + 0.1f && !requested; t += Dt)
            {
                frame = Step(Body(170f, space: false));
                requested = frame.RequestRespawn;
            }
            Assert.IsTrue(requested);
            Assert.AreEqual(RespawnReason.NotRecoverable, frame.RespawnReason);
        }

        [Test]
        public void Blink_CancelsWhenTheCarRecovers()
        {
            Run(Body(170f, space: false), _settings.FlipDetectTime + 0.5f);
            StabilityFrame frame = Run(Wheels(4), 2f);
            Assert.IsFalse(frame.RecoveryWarning || frame.RequestRespawn);
        }

        [Test]
        public void Cooldown_BlocksASecondImmediateAttempt()
        {
            Run(Body(170f), _settings.FlipDetectTime + Dt);
            Step(Wheels(4, 5f));
            Run(Body(170f), _settings.FlipDetectTime + Dt);
            Assert.AreNotEqual(VehicleStabilityState.SelfRecovery, _fsm.State);
        }

        [Test]
        public void FlippedInTheAir_IsNotAFlip()
        {
            StabilityInput input = Air(-2f);
            input.Tilt = 180f;
            StabilityFrame frame = Run(input, 5f);
            Assert.AreEqual(VehicleStabilityState.Airborne, frame.State);
            Assert.IsFalse(frame.RequestRespawn);
        }

        [Test]
        public void Stranded_OnTwoWheels_AsksForHelp_ButNotWhileMoving()
        {
            StabilityFrame frame = Run(Wheels(2, 5f, Vector3.forward * 10f), 4f);
            Assert.IsFalse(frame.RecoveryWarning);
            _fsm.Reset();
            bool attempted = false;
            for (float t = 0f; t < 3f; t += Dt) attempted |= Step(Wheels(2, 5f)).RequestRecoveryImpulse;
            Assert.IsTrue(attempted);
        }

        [Test]
        public void Reset_ForgetsEverything()
        {
            Run(Body(170f), _settings.FlipDetectTime + Dt);
            _fsm.Reset();
            StabilityFrame frame = Step(Air(-1f));
            Assert.AreEqual(VehicleStabilityState.Airborne, frame.State);
            Assert.AreEqual(0, frame.LandingEpisode);
            Assert.IsFalse(frame.RecoveryWarning || frame.RequestRecoveryImpulse || frame.RequestRespawn || frame.JustLostSupport);
        }

        [TestCase(0.02f)]
        [TestCase(0.0166667f)]
        [TestCase(0.01f)]
        [TestCase(0.0083333f)]
        public void Timers_DoNotDependOnTheStep(float dt)
        {
            float elapsed = 0f;
            while (_fsm.State != VehicleStabilityState.Grounded && elapsed < 1f)
            {
                _fsm.Evaluate(Wheels(4), _settings, dt);
                elapsed += dt;
            }
            Assert.AreEqual(_settings.GroundedConfirmationTime + _settings.LandingGripBlendTime, elapsed, 0.041f);
        }

        #endregion
    }
}
