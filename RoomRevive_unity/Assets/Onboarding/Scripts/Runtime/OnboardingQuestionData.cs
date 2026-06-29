using System.Collections.Generic;
using UnityEngine;

namespace RoomRevive.Onboarding
{
    [CreateAssetMenu(menuName = "RoomRevive/Onboarding/Question Data", fileName = "QuestionData")]
    public class OnboardingQuestionData : ScriptableObject
    {
        [Tooltip("Question shown in the banner, e.g. 'Which style do you prefer?'")]
        public string prompt;

        [Tooltip("Progress label shown below the question, e.g. '1 of 4'")]
        public string stepLabel;

        [Tooltip("True = 2x2 image card grid (Q1/Q2). False = full-width text rows (Q3/Q4).")]
        public bool useImageCards;

        public List<OnboardingOptionData> options = new();
    }
}
