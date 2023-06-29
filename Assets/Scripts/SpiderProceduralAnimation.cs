using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiderProceduralAnimation : MonoBehaviour
{
    public Transform[] legTargets; // Les points de contact des pattes
    public float stepSize = 0.15f; // La distance entre le point de contact et le point de contact suivant
    public int smoothness = 8; // Le nombre de frames pour effectuer un pas
    public float stepHeight = 0.15f; // La hauteur du pas
    public float sphereCastRadius = 0.125f; // Le rayon du raycast
    public bool bodyOrientation = true; // Si le corps doit s'orienter vers la direction du mouvement
    public float raycastRange = 1.5f; // La distance maximale du raycast

    [Header("Advanced Settings - Debug"), Space(10)]
    [SerializeField] private Vector3[] _defaultLegPositions; // Les positions de base des points de contact
    [SerializeField] private Vector3[] _lastLegPositions; // Les positions des points de contact à la frame précédente
    [SerializeField] private Vector3 _lastBodyUp; // La direction du corps à la frame précédente
    [SerializeField] private bool[] _legMoving; // Si la patte est en train de bouger
    [SerializeField] private int _nbLegs; // Le nombre de pattes
    
    [SerializeField] private Vector3 _velocity; // La vitesse du corps
    [SerializeField] private Vector3 _lastVelocity; // La vitesse du corps à la frame précédente
    [SerializeField] private Vector3 _lastBodyPos; // La position du corps à la frame précédente

    private readonly float _velocityMultiplier = 15f; // Le multiplicateur de vitesse

    Vector3[] MatchToSurfaceFromAbove(Vector3 point, float halfRange, Vector3 up)
    {
        Vector3[] res = new Vector3[2];
        res[1] = Vector3.zero;
        RaycastHit hit;
        Ray ray = new Ray(point + halfRange * up / 2f, - up);

        if (Physics.SphereCast(ray, sphereCastRadius, out hit, 2f * halfRange))
        {
            res[0] = hit.point;
            res[1] = hit.normal;
        }
        else
        {
            res[0] = point;
        }
        return res;
    }
    
    void Start()
    {
        _lastBodyUp = transform.up; // On initialise la direction du corps

        _nbLegs = legTargets.Length; // On initialise le nombre de pattes
        _defaultLegPositions = new Vector3[_nbLegs]; // On initialise les tableaux
        _lastLegPositions = new Vector3[_nbLegs]; // On initialise les tableaux
        _legMoving = new bool[_nbLegs]; // On initialise les tableaux
        
        // On initialise les tableaux
        for (int i = 0; i < _nbLegs; ++i)
        {
            _defaultLegPositions[i] = legTargets[i].localPosition; // On récupère les positions de base des points de contact
            _lastLegPositions[i] = legTargets[i].position; // On initialise les positions des points de contact à la frame précédente
            _legMoving[i] = false; // On initialise les booléens
        }
        
        // On initialise les variables
        _lastBodyPos = transform.position;
    }

    IEnumerator PerformStep(int index, Vector3 targetPoint)
    {
        Vector3 startPos = _lastLegPositions[index];
        for(int i = 1; i <= smoothness; ++i)
        {
            legTargets[index].position = Vector3.Lerp(startPos, targetPoint, i / (float)(smoothness + 1f));
            legTargets[index].position += transform.up * (Mathf.Sin(i / (float)(smoothness + 1f) * Mathf.PI) * stepHeight);
            yield return new WaitForFixedUpdate();
        }
        legTargets[index].position = targetPoint;
        _lastLegPositions[index] = legTargets[index].position;
        _legMoving[0] = false;
    }


    void FixedUpdate()
    {
        _velocity = transform.position - _lastBodyPos;
        _velocity = (_velocity + smoothness * _lastVelocity) / (smoothness + 1f);

        if (_velocity.magnitude < 0.000025f)
            _velocity = _lastVelocity;
        else
            _lastVelocity = _velocity;
        
        
        Vector3[] desiredPositions = new Vector3[_nbLegs];
        int indexToMove = -1;
        float maxDistance = stepSize;
        for (int i = 0; i < _nbLegs; ++i)
        {
            desiredPositions[i] = transform.TransformPoint(_defaultLegPositions[i]);

            float distance = Vector3.ProjectOnPlane(desiredPositions[i] + _velocity * _velocityMultiplier - _lastLegPositions[i], transform.up).magnitude;
            if (distance > maxDistance)
            {
                maxDistance = distance;
                indexToMove = i;
            }
        }
        for (int i = 0; i < _nbLegs; ++i)
            if (i != indexToMove)
                legTargets[i].position = _lastLegPositions[i];

        if (indexToMove != -1 && !_legMoving[0])
        {
            Vector3 targetPoint = desiredPositions[indexToMove] + Mathf.Clamp(_velocity.magnitude * _velocityMultiplier, 0.0f, 1.5f) * (desiredPositions[indexToMove] - legTargets[indexToMove].position) + _velocity * _velocityMultiplier;

            Vector3[] positionAndNormalFwd = MatchToSurfaceFromAbove(targetPoint + _velocity * _velocityMultiplier, raycastRange, (transform.parent.up - _velocity * 100).normalized);
            Vector3[] positionAndNormalBwd = MatchToSurfaceFromAbove(targetPoint + _velocity * _velocityMultiplier, raycastRange*(1f + _velocity.magnitude), (transform.parent.up + _velocity * 75).normalized);
            
            _legMoving[0] = true;
            
            if (positionAndNormalFwd[1] == Vector3.zero)
            {
                StartCoroutine(PerformStep(indexToMove, positionAndNormalBwd[0]));
            }
            else
            {
                StartCoroutine(PerformStep(indexToMove, positionAndNormalFwd[0]));
            }
        }

        _lastBodyPos = transform.position;
        if (_nbLegs > 3 && bodyOrientation)
        {
            Vector3 v1 = legTargets[0].position - legTargets[1].position;
            Vector3 v2 = legTargets[2].position - legTargets[3].position;
            Vector3 normal = Vector3.Cross(v1, v2).normalized;
            Vector3 up = Vector3.Lerp(_lastBodyUp, normal, 1f / (float)(smoothness + 1));
            transform.up = up;
            transform.rotation = Quaternion.LookRotation(transform.parent.forward, up);
            _lastBodyUp = transform.up;
        }
    }

    private void OnDrawGizmos()
    {
        if (_nbLegs > 0)
        {
            for (int i = 0; i < _nbLegs; ++i)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(legTargets[i].position, 0.05f);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.TransformPoint(_defaultLegPositions[i]), stepSize);
                
                // Faire une ligne entre les deux
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(legTargets[i].position, transform.TransformPoint(_defaultLegPositions[i]));
                
                
            }
        }
    }
}
