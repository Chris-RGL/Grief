using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIchase : MonoBehaviour
{
    public GameObject Player;

    [Header("Seeking")]
    public float speed;

    [Header("Bounce Parameters")]
    public float timeToRebound;

    // Private variables
    private Rigidbody _rb;
    private Transform _playerTransform;
    private Vector3 _targetDirection;
    private float _timeToChangeDirection;
    private Vector3 _bounceDirection;
    private float _reboundTime;
    private float _reboundSpeed;

    // Calculated Movement
    private Quaternion _nextRotationAngle = new Quaternion();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (_playerTransform == null)
        {
            // problem
            return;
        }

        // Calc dist to player
        float distance = Vector3.Distance(_rb.position, _playerTransform.position);
    }

    private void FixedUpdate()
    {
        if (_playerTransform == null) return;

        Vector3 directionToPlayer = ((Vector3)_playerTransform.position - _rb.position).normalized;
        Vector3 finalVelocity = directionToPlayer * speed;

        if (_reboundTime > 0f)
        {
            Vector3 reboundVelocity = _reboundTime * _bounceDirection * _reboundSpeed;
            finalVelocity = finalVelocity + reboundVelocity;
            _rb.velocity = finalVelocity;
            _reboundTime -= Time.fixedDeltaTime;
        }
        else
        {
            finalVelocity = directionToPlayer * speed;
            _rb.velocity = finalVelocity;
        }

        Debug.DrawRay(_rb.position, finalVelocity, Color.red);

        _nextRotationAngle = Quaternion.LookRotation(finalVelocity);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ContactPoint contact = collision.GetContact(0);
        Vector3 directionToContact = contact.point - _rb.position;
        _bounceDirection = Vector3.Reflect(directionToContact, contact.normal).normalized;
        _reboundTime = timeToRebound;
    }
}
