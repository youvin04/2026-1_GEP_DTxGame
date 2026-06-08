using System.Collections;
using TMPro;
using UnityEngine;

public class TypewriterEffect : MonoBehaviour
{
    public TextMeshProUGUI targetText;
    public float charDelay = 0.05f;  // 글자 사이 간격 (초)

    private Coroutine _typing;
    private bool _isTyping = false;

    public void ShowText(string text)
    {
        if (_typing != null) StopCoroutine(_typing);
        _typing = StartCoroutine(TypeRoutine(text));
    }

    // 탭하면 타이핑 스킵 (바로 전체 표시)
    public void SkipOrNext()
    {
        if (_typing != null)  // null 체크 추가
        {
            StopCoroutine(_typing);
            _typing = null;
        }
        _isTyping = false;
        targetText.maxVisibleCharacters = targetText.text.Length;
    }

    public bool IsTyping => _isTyping;

    IEnumerator TypeRoutine(string text)
    {
        _isTyping = true;
        targetText.text = text;
        targetText.maxVisibleCharacters = 0;

        for (int i = 0; i <= text.Length; i++)
        {
            targetText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(charDelay);
        }

        _isTyping = false;
    }
}