using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Walking Script должен быть примерно таким:
public class WalkingScript : MonoBehaviour
{
    public Animator anim;
    public float moveSpeed = 3f;

    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Движение
        Vector3 move = new Vector3(horizontal, 0, vertical);
        transform.position += move * moveSpeed * Time.deltaTime;

        // Анимация
        bool isWalking = move.magnitude > 0.1f;
        anim.SetBool("isWalking", isWalking);
    }
}
