using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

public class SlideToCall : MonoBehaviour, 
    IDragHandler, IEndDragHandler, IBeginDragHandler
{
    public RectTransform handle;
    public RectTransform track;
    public float triggerThreshold = 0.8f;
    public CallManager callManager;

    private float handleStartX;
    private float maxSlideDistance;
    private bool triggered = false;
    private Vector2 dragStartPos;

    void Start()
    {
        handleStartX = handle.anchoredPosition.x;
        maxSlideDistance = track.rect.width 
            - handle.rect.width - 16f;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (triggered) return;
        dragStartPos = e.position;
    }

    public void OnDrag(PointerEventData e)
    {
        if (triggered) return;
        float delta = Mathf.Clamp(
            e.position.x - dragStartPos.x, 0f, maxSlideDistance);
        handle.anchoredPosition = new Vector2(
            handleStartX + delta, handle.anchoredPosition.y);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (triggered) return;
        float ratio = (handle.anchoredPosition.x - handleStartX) 
            / maxSlideDistance;

        if (ratio >= triggerThreshold)
        {
            triggered = true;
            StartCoroutine(TriggerSequence());
        }
        else
        {
            StartCoroutine(SpringBack());
        }
    }

    IEnumerator TriggerSequence()
    {
        float elapsed = 0f;
        Vector2 start = handle.anchoredPosition;
        Vector2 end = new Vector2(
            handleStartX + maxSlideDistance, 
            handle.anchoredPosition.y);

        while (elapsed < 0.15f)
        {
            elapsed += Time.deltaTime;
            handle.anchoredPosition = Vector2.Lerp(start, end, elapsed / 0.15f);
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);
        callManager.OnCallAccepted();
    }

    IEnumerator SpringBack()
    {
        float elapsed = 0f;
        Vector2 start = handle.anchoredPosition;
        Vector2 ret = new Vector2(
            handleStartX, handle.anchoredPosition.y);

        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Pow(1f - elapsed / 0.3f, 3f);
            handle.anchoredPosition = Vector2.Lerp(start, ret, t);
            yield return null;
        }
        handle.anchoredPosition = ret;
    }
}