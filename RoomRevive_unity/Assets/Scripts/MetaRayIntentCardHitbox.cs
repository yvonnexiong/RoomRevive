using UnityEngine;
using UnityEngine.EventSystems;

public class MetaRayIntentCardHitbox : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerClickHandler
{
    [SerializeField] private MetaRayIntentCardMenu menu;
    [SerializeField] private int cardIndex;

    public void Initialize(MetaRayIntentCardMenu owner, int index)
    {
        menu = owner;
        cardIndex = index;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (menu == null) return;
        menu.SetHoveredCard(cardIndex, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (menu == null) return;
        menu.SetHoveredCard(cardIndex, false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (menu == null) return;

        if (menu.selectCardOnPointerDown)
            menu.SelectCard(cardIndex);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (menu == null) return;

        if (menu.selectCardOnPointerClick)
            menu.SelectCard(cardIndex);
    }
}
