using UnityEngine;
using TMPro;

public class ArenaTimer : MonoBehaviour
{
    public float duration = 60f;
    public TMP_Text timerText;

    private float _remaining;
    private bool _running;

    public void StartTimer(float seconds)
    {
        duration = seconds;
        _remaining = seconds;
        _running = true;
    }

    void Update()
    {
        if (!_running) return;
        _remaining -= Time.deltaTime;
        if (_remaining <= 0)
        {
            _remaining = 0;
            _running = false;
            GameManager.Instance.TriggerGameOver();
        }
        if (timerText != null)
            timerText.text = $"{Mathf.CeilToInt(_remaining)}s";
    }

    public int GetElapsed() => Mathf.RoundToInt(duration - _remaining);
}