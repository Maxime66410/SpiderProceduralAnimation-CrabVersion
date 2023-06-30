using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Faire en sorte que on se déplace 2 pattes à la fois

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
        _legMoving = new bool[_nbLegs / 2]; // On initialise les tableaux
        
        // On initialise les tableaux
        for (int i = 0; i < _nbLegs; ++i)
        {
            _defaultLegPositions[i] = legTargets[i].localPosition; // On récupère les positions de base des points de contact
            _lastLegPositions[i] = legTargets[i].position; // On initialise les positions des points de contact à la frame précédente
            _legMoving[i / 2] = false; // On initialise les booléens
        }
        
        // On initialise les variables
        _lastBodyPos = transform.position;
    }

    // On effectue un pas
    IEnumerator PerformStep(int firstIndex, int secondIndex, Vector3 firstTargetPoint, Vector3 secondTargetPoint)
    {
        // Si la patte est déjà dans la bonne position
        if( Vector3.Distance(legTargets[firstIndex].position, firstTargetPoint) < 0.1f ||
            Vector3.Distance(legTargets[secondIndex].position, secondTargetPoint) < 0.1f)
        {
            _legMoving[firstIndex / 2] = false;
            yield break;
        }
        
        Vector3 firstStartPos = _lastLegPositions[firstIndex];
        Vector3 secondStartPos = _lastLegPositions[secondIndex];

        for (int i = 1; i <= smoothness; ++i)
        {
            float t = i / (float)(smoothness + 1f);
            legTargets[firstIndex].position = Vector3.Lerp(firstStartPos, firstTargetPoint, t);
            legTargets[secondIndex].position = Vector3.Lerp(secondStartPos, secondTargetPoint, t);

            legTargets[firstIndex].position += transform.up * (Mathf.Sin(t * Mathf.PI) * stepHeight);
            legTargets[secondIndex].position += transform.up * (Mathf.Sin(t * Mathf.PI) * stepHeight);

            yield return new WaitForFixedUpdate();
        }

        legTargets[firstIndex].position = firstTargetPoint;
        legTargets[secondIndex].position = secondTargetPoint;

        _lastLegPositions[firstIndex] = legTargets[firstIndex].position;
        _lastLegPositions[secondIndex] = legTargets[secondIndex].position;

        _legMoving[firstIndex / 2] = false;
    }


    void FixedUpdate()
    {
        _velocity = transform.position - _lastBodyPos; // On calcule la vitesse du corps
        _velocity = (_velocity + smoothness * _lastVelocity) / (smoothness + 1f); // On lisse la vitesse du corps

        // On calcule la direction du corps
        if (_velocity.magnitude < 0.000025f) // Si la vitesse est trop faible
            _velocity = _lastVelocity;
        else
            _lastVelocity = _velocity;
        
        
        Vector3[] desiredPositions = new Vector3[_nbLegs]; // On initialise le tableau
        int indexToMove = -1; // On initialise la variable
        float maxDistance = stepSize; // On initialise la variable
        // On calcule les positions des points de contact
        for (int i = 0; i < _nbLegs; ++i) 
        {
            desiredPositions[i] = transform.TransformPoint(_defaultLegPositions[i]); // On calcule la position du point de contact

            // On calcule la distance entre la position du point de contact et la position du point de contact à la frame précédente
            float distance = Vector3.ProjectOnPlane(desiredPositions[i] + _velocity * _velocityMultiplier - _lastLegPositions[i], transform.up).magnitude; 
            
            // On met à jour la variable
            if (distance > maxDistance)
            {
                maxDistance = distance;
                indexToMove = i;
            }
        }
        
        // On calcule la direction du corps
        for (int i = 0; i < _nbLegs; ++i)
            if (i != indexToMove) // Si la patte n'est pas en train de bouger
                legTargets[i].position = _lastLegPositions[i]; // On met à jour la position du point de contact

        // On effectue le pas
        if (indexToMove != -1 && !_legMoving[indexToMove / 2])
        {
            int firstIndex = indexToMove;
            int secondIndex = 0;
            Vector3 targetPoint2 = Vector3.zero;
            Vector3[] positionAndNormalFwdSecond = Array.Empty<Vector3>();
            Vector3[] positionAndNormalBwdSecond = Array.Empty<Vector3>();

            if (indexToMove + 1 < _nbLegs)
            {
                secondIndex = indexToMove + 1;
            }
            else
            {
                secondIndex = indexToMove - 1;
            }

            // Avec desiredPositions[indexToMove] plus Mathf.Clamp pour éviter que la patte ne se déplace trop loin
            Vector3 targetPoint = desiredPositions[firstIndex] + Mathf.Clamp(_velocity.magnitude * _velocityMultiplier, 0.0f, 1.5f) * (desiredPositions[firstIndex] - legTargets[firstIndex].position) + _velocity * _velocityMultiplier;
            targetPoint2 = desiredPositions[secondIndex] + Mathf.Clamp(_velocity.magnitude * _velocityMultiplier, 0.0f, 1.5f) * (desiredPositions[secondIndex] - legTargets[secondIndex].position) + _velocity * _velocityMultiplier;

            // On calcule la position du point de contact
            Vector3[] positionAndNormalFwd = MatchToSurfaceFromAbove(targetPoint + _velocity * _velocityMultiplier, raycastRange, (transform.parent.up - _velocity * 100).normalized);
            // On calcule la position du point de contact avec la patte en arrière
            Vector3[] positionAndNormalBwd = MatchToSurfaceFromAbove(targetPoint + _velocity * _velocityMultiplier, raycastRange*(1f + _velocity.magnitude), (transform.parent.up + _velocity * 75).normalized);
            
            positionAndNormalFwdSecond = MatchToSurfaceFromAbove(targetPoint2 + _velocity * _velocityMultiplier, raycastRange, (transform.parent.up - _velocity * 100).normalized);
            
            positionAndNormalBwdSecond = MatchToSurfaceFromAbove(targetPoint2 + _velocity * _velocityMultiplier, raycastRange*(1f + _velocity.magnitude), (transform.parent.up + _velocity * 75).normalized);
            
            _legMoving[firstIndex / 2] = true;
            
            // On effectue le pas
            if (positionAndNormalFwd[1] == Vector3.zero) // Si la patte est en l'air
            {
                StartCoroutine(PerformStep(firstIndex, secondIndex, positionAndNormalBwd[0], positionAndNormalBwdSecond[0]));
            }
            else
            {
                StartCoroutine(PerformStep(firstIndex, secondIndex, positionAndNormalFwd[0], positionAndNormalFwdSecond[0]));
            }
        }

        _lastBodyPos = transform.position;
        // On calcule l'orientation du corps
        if (_nbLegs > 3 && bodyOrientation)
        {
            Vector3 v1 = legTargets[0].position - legTargets[1].position; // On calcule les vecteurs pour calculer la normale
            Vector3 v2 = legTargets[2].position - legTargets[3].position;
            Vector3 normal = Vector3.Cross(v1, v2).normalized; // On calcule la normale avec les deux vecteurs en paramètre avec du Cross pour avoir la normale
            Vector3 up = Vector3.Lerp(_lastBodyUp, normal, 1f / (float)(smoothness + 1));  // On lisse la normale
            transform.up = up;  
            transform.rotation = Quaternion.LookRotation(transform.parent.forward, up); // On calcule la rotation du corps
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
