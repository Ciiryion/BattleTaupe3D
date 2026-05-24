using UnityEngine;

public class Bullet : MonoBehaviour
{
    public int damage = 1;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
            other.GetComponent<EnemyController>()?.TakeDamage(damage);
    }
}