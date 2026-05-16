using System;
using UnityEngine.Events;

namespace RoomRevive.IntentSelector
{
    [Serializable] public class IntentStateEvent : UnityEvent<IntentStateData> { }

    [Serializable] public class IntentIndexEvent : UnityEvent<int> { }
}
