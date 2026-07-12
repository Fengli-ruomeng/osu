// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Overlays.Settings.Sections.Audio;

namespace osu.Game.Tests.Settings
{
    [TestFixture]
    public class OffsetCalibrationSessionTest
    {
        [Test]
        public void TestConsistentRoundsProduceReliableSuggestion()
        {
            var session = new OffsetCalibrationSession(10, 15);

            completeRound(session, 35);

            Assert.That(session.State, Is.EqualTo(OffsetCalibrationSession.CalibrationState.BetweenRounds));
            Assert.That(session.RoundResults[0].SuggestedOffset, Is.EqualTo(-50));

            completeRound(session, 37);

            Assert.That(session.State, Is.EqualTo(OffsetCalibrationSession.CalibrationState.Completed));
            Assert.That(session.IsReliable, Is.True);
            Assert.That(session.SuggestedOffset, Is.EqualTo(-51));
        }

        [Test]
        public void TestInconsistentRoundsAreRejected()
        {
            var session = new OffsetCalibrationSession(0, 0);

            completeRound(session, 20);
            completeRound(session, 45);

            Assert.That(session.IsReliable, Is.False);
            Assert.That(session.SuggestedOffset, Is.EqualTo(-32));
        }

        [Test]
        public void TestWarmupDuplicateAndDistantTapsAreIgnored()
        {
            var session = new OffsetCalibrationSession(0, 0);
            session.StartRound(1000);

            Assert.That(session.RegisterTap(1000), Is.False);

            double firstTarget = 1000 + OffsetCalibrationSession.WARMUP_BEATS * OffsetCalibrationSession.BEAT_LENGTH;
            session.RegisterBeat(OffsetCalibrationSession.WARMUP_BEATS, firstTarget);
            Assert.That(session.RegisterTap(firstTarget + 20), Is.True);
            Assert.That(session.RegisterTap(firstTarget + 30), Is.False);
            session.RegisterBeat(OffsetCalibrationSession.WARMUP_BEATS + 1, firstTarget + OffsetCalibrationSession.BEAT_LENGTH);
            Assert.That(session.RegisterTap(firstTarget + OffsetCalibrationSession.BEAT_LENGTH + 220), Is.False);
            Assert.That(session.RecordedTaps, Is.EqualTo(1));
        }

        [Test]
        public void TestEarlyTapCanMatchUpcomingBeat()
        {
            var session = new OffsetCalibrationSession(0, 0);
            session.StartRound(0);

            double lastWarmupBeat = (OffsetCalibrationSession.WARMUP_BEATS - 1) * OffsetCalibrationSession.BEAT_LENGTH;
            session.RegisterBeat(OffsetCalibrationSession.WARMUP_BEATS - 1, lastWarmupBeat);

            Assert.That(session.RegisterTap(lastWarmupBeat + OffsetCalibrationSession.BEAT_LENGTH - 20), Is.True);
            Assert.That(session.RecordedTaps, Is.EqualTo(1));
        }

        [Test]
        public void TestNoisyRoundIsRejected()
        {
            var session = new OffsetCalibrationSession(0, 0);

            session.StartRound(0);
            for (int i = 0; i < OffsetCalibrationSession.TAPS_PER_ROUND; i++)
            {
                session.RegisterBeat(OffsetCalibrationSession.WARMUP_BEATS + i, targetTime(i));
                Assert.That(session.RegisterTap(targetTime(i) + (i % 2 == 0 ? -60 : 60)), Is.True);
            }

            completeRound(session, 0);

            Assert.That(session.RoundResults[0].Spread, Is.GreaterThan(30));
            Assert.That(session.IsReliable, Is.False);
        }

        private static void completeRound(OffsetCalibrationSession session, double error)
        {
            session.StartRound(0);

            for (int i = 0; i < OffsetCalibrationSession.TAPS_PER_ROUND; i++)
            {
                session.RegisterBeat(OffsetCalibrationSession.WARMUP_BEATS + i, targetTime(i));
                Assert.That(session.RegisterTap(targetTime(i) + error), Is.True);
            }
        }

        private static double targetTime(int tapIndex) =>
            (OffsetCalibrationSession.WARMUP_BEATS + tapIndex) * OffsetCalibrationSession.BEAT_LENGTH;
    }
}
