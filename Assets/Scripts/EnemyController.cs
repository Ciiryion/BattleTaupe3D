using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public int hp = 2;

    public void TakeDamage(int dmg)
    {
        hp -= dmg;
        if (hp <= 0)
        {
            GameManager.Instance?.AddScore(1);
            Destroy(gameObject);
        }
    }
}