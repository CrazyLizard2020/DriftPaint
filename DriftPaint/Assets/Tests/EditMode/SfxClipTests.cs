using System.Collections.Generic;
using DriftPaint.Audio;
using NUnit.Framework;
using UnityEngine;

namespace DriftPaint.Tests
{
    public class SfxClipTests
    {
        AudioClip[] _clips;

        [SetUp]
        public void CreateClips()
        {
            _clips = new AudioClip[3];
            for (int i = 0; i < _clips.Length; i++) _clips[i] = AudioClip.Create($"Variante {i}", 64, 1, 32000, false);
        }

        [TearDown]
        public void DestroyClips()
        {
            foreach (AudioClip clip in _clips) Object.DestroyImmediate(clip);
        }

        [Test]
        public void Shuffle_PlaysEveryVariantBeforeRepeating()
        {
            var sfx = new SfxClip(_clips, 1f, 0f, SfxSelection.Shuffle);
            for (int round = 0; round < 20; round++)
            {
                var seen = new HashSet<AudioClip>();
                for (int i = 0; i < _clips.Length; i++) seen.Add(sfx.Pick());
                Assert.AreEqual(_clips.Length, seen.Count, "En cada vuelta suenan todas las variantes.");
            }
        }

        [TestCase(SfxSelection.Shuffle)]
        [TestCase(SfxSelection.RandomNoRepeat)]
        [TestCase(SfxSelection.Sequence)]
        public void NoRepeatModes_NeverPlayTheSameVariantTwiceInARow(SfxSelection selection)
        {
            var sfx = new SfxClip(_clips, 1f, 0f, selection);
            AudioClip previous = sfx.Pick();
            for (int i = 0; i < 300; i++)
            {
                AudioClip next = sfx.Pick();
                Assert.AreNotSame(previous, next);
                previous = next;
            }
        }

        [Test]
        public void Sequence_FollowsTheListOrder()
        {
            var sfx = new SfxClip(_clips, 1f, 0f, SfxSelection.Sequence);
            for (int i = 0; i < 7; i++) Assert.AreSame(_clips[i % _clips.Length], sfx.Pick());
        }

        [Test]
        public void PitchAndVolume_StayWithinTheirVariation()
        {
            var sfx = new SfxClip(_clips, 0.6f, 0.05f, SfxSelection.Random, volumeVariation: 0.1f);
            for (int i = 0; i < 200; i++)
            {
                Assert.That(sfx.PickPitch(), Is.InRange(0.95f, 1.05f));
                Assert.That(sfx.PickVolume(), Is.InRange(0.54f, 0.66f));
            }
        }
    }
}
