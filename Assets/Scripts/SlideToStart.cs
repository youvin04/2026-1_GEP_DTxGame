using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

public class SlideToStart : MonoBehaviour, IDragHandler, IEndDragHandler, IBeginDragHandler
{
    [Header("슬라이드 설정")]
    public RectTransform handle;          // 밀어서 통화하기 핸들 (전화기 아이콘 포함 원형 버튼)
    public RectTransform track;           // 슬라이드 배경 트랙
    public float triggerThreshold = 0.8f; // 이 비율 이상 밀면 발동 (0~1)

    [Header("연결")]
    public StartSceneManager sceneManager;

    private float trackWidth;
    private float handleStartX;
    private float maxSlideDistance;
    private bool triggered = false;
    private Vector2 dragStartPos;

    void Start()
    {
        // 트랙 너비와 핸들 초기 위치 계산
        trackWidth = track.rect.width;
        handleStartX = handle.anchoredPosition.x;
        // 핸들이 트랙 오른쪽 끝까지 이동 가능한 최대 거리
        maxSlideDistance = trackWidth - handle.rect.width - 16f;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (triggered) return;
        dragStartPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (triggered) return;

        // 드래그 거리 계산 (오른쪽으로만)
        float dragDelta = eventData.position.x - dragStartPos.x;
        dragDelta = Mathf.Clamp(dragDelta, 0f, maxSlideDistance);

        // 핸들 위치 업데이트
        handle.anchoredPosition = new Vector2(handleStartX + dragDelta, handle.anchoredPosition.y);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (triggered) return;

        float currentSlide = handle.anchoredPosition.x - handleStartX;
        float slideRatio = currentSlide / maxSlideDistance;

        if (slideRatio >= triggerThreshold)
        {
            // 끝까지 밀었을 때 — 씬 이동
            triggered = true;
            StartCoroutine(TriggerSequence());
        }
        else
        {
            // 덜 밀었을 때 — 스프링 백
            StartCoroutine(SpringBack());
        }
    }

    IEnumerator TriggerSequence()
    {
        // 핸들을 끝으로 이동
        float elapsed = 0f;
        Vector2 startPos = handle.anchoredPosition;
        Vector2 endPos = new Vector2(handleStartX + maxSlideDistance, handle.anchoredPosition.y);

        while (elapsed < 0.15f)
        {
            elapsed += Time.deltaTime;
            handle.anchoredPosition = Vector2.Lerp(startPos, endPos, elapsed / 0.15f);
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        // 씬 매니저 호출
        if (sceneManager != null)
            sceneManager.OnStartButton();
    }

    IEnumerator SpringBack()
    {
        // 스프링 백 애니메이션
        float elapsed = 0f;
        Vector2 startPos = handle.anchoredPosition;
        Vector2 returnPos = new Vector2(handleStartX, handle.anchoredPosition.y);

        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Pow(1f - elapsed / 0.3f, 3f); // ease out
            handle.anchoredPosition = Vector2.Lerp(startPos, returnPos, t);
            yield return null;
        }
        handle.anchoredPosition = returnPos;
    }
}