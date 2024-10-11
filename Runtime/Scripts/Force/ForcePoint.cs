using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using DefaultNamespace.Water;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEditor.EditorTools;

// This is a very simple example of how we could compute a buoyancy force at variable points along the body.
// Its not really accurate per se.
// [RequireComponent(typeof(Rigidbody))]
// [RequireComponent(typeof(IForceModel))]
namespace Force
{

    // Because Arti bodies and Rigid bodies dont share
    // an ancestor, even though they share like 99% of the
    // methods and semantics...
    public class MixedBody
    {
        public ArticulationBody ab;
        public Rigidbody rb;

        public bool automaticCenterOfMass
        {
            get {return ab ? ab.automaticCenterOfMass : rb.automaticCenterOfMass; }
            set {
                if(ab != null) ab.automaticCenterOfMass = value;
                else rb.automaticCenterOfMass = value;
                }
        }

        public Vector3 centerOfMass
        {
            get {return ab ? ab.centerOfMass : rb.centerOfMass; }
            set {
                if(ab != null) ab.centerOfMass = value;
                else rb.centerOfMass = value;
            }
        }

        public bool useGravity
        {
            get {return ab ? ab.useGravity : rb.useGravity; }
            set {
                if(ab != null) ab.useGravity = value;
                else rb.useGravity = value;
            }
        }

        public float mass
        {
            get {return ab ? ab.mass : rb.mass; }
            set {
                if(ab != null) ab.mass = value;
                else rb.mass = value;
            }
        }

        public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode mode = ForceMode.Force)
        {
            if(ab != null)
                ab.AddForceAtPosition(force, position, mode);
            else
                rb.AddForceAtPosition(force, position, mode);
        }
    }

    public class ForcePoint : MonoBehaviour
    {
        [Header("Connected Body")]
        public ArticulationBody ConnectedArticulationBody;
        public Rigidbody ConnectedRigidbody;


        [Header("Buoyancy")]
        [Tooltip("GameObject that we will calculate the volume of. Set volume below to 0 to use.")]
        public GameObject VolumeObject;
        [Tooltip("If the gameObject above has many meshes, set the one to use for volume calculations here.")]
        public Mesh VolumeMesh;
        [Tooltip("If not zero, will be used for buoyancy calculations. If zero, the volumeObject/Mesh above will be used to calculate.")]
        public float Volume;
        public float WaterDensity = 997; // kg/m3
        public float DepthBeforeSubmerged = 0.03f;
        public float MaxBuoyancyForce = 1000f;


        [Header("Gravity")]
        [Tooltip("Do we over-ride the gravity of the connected body?")]
        public bool AddGravity = false;
        [Tooltip("If true, calculates center of gravity from all the ForcePoints on the body and overrides the body's centerOfMass, otherwise the centerOfMass of the connected body is used.")]
        public bool AutomaticCenterOfGravity = false;
        [Tooltip("If not zero, will be used for gravity force. If zero, the connected body's mass will be used instead.")]
        public float Mass;


        [Header("Debug")]
        public bool DrawForces = false;


        private MixedBody _body;
        private int _pointCount;
        private WaterQueryModel _waterModel;

        public void ApplyCurrent(Vector3 current)
        {
            float waterSurfaceLevel = _waterModel.GetWaterLevelAt(transform.position);
            if (transform.position.y < waterSurfaceLevel)
            {
                _body.AddForceAtPosition
                        (
                            current / _pointCount,
                            transform.position,
                            ForceMode.Force
                        );
            }
        }


        public void Awake()
        {
            _body = new MixedBody();
            
            if(ConnectedArticulationBody == null && ConnectedRigidbody == null)
                Debug.LogWarning($"{gameObject.name} requires at least one of ConnectedArticulationBody or ConnectedRigidBody to be set!");
            
            if(ConnectedArticulationBody != null) _body.ab = ConnectedArticulationBody;
            if(ConnectedRigidbody!= null) _body.rb = ConnectedRigidbody;

             // If the force point is doing the gravity, disable the body's own
            if(AddGravity)
            {
                _body.useGravity = false;
                if(Mass == 0) Mass = _body.mass;
            }
            
            
            _waterModel = FindObjectsByType<WaterQueryModel>(FindObjectsSortMode.None)[0];

            var forcePoints = transform.root.gameObject.GetComponentsInChildren<ForcePoint>();
            if (AutomaticCenterOfGravity)
            {
                _body.automaticCenterOfMass = false;
                var centerOfMass = forcePoints.Select(point => point.transform.localPosition).Aggregate(new Vector3(0, 0, 0), (s, v) => s + v);
                _body.centerOfMass = centerOfMass / forcePoints.Length;
            }

            _pointCount = forcePoints.Length;
           
            if (VolumeMesh == null && VolumeObject != null) VolumeMesh = VolumeObject.GetComponent<MeshFilter>().mesh;
            if (Volume == 0 && VolumeMesh != null) Volume = MeshVolume.CalculateVolumeOfMesh(VolumeMesh, VolumeObject.transform.lossyScale);
        }

        // Volume * Density * Gravity
        private void FixedUpdate()
        {
            var forcePointPosition = transform.position;
            if (AddGravity)
            {
                Vector3 gravityForce = Mass * Physics.gravity / _pointCount;
                _body.AddForceAtPosition(gravityForce, forcePointPosition, ForceMode.Force);
                if(DrawForces) Debug.DrawLine(forcePointPosition, forcePointPosition+gravityForce, Color.red, 0.1f);
            }


            float waterSurfaceLevel = _waterModel.GetWaterLevelAt(forcePointPosition);
            float depth = waterSurfaceLevel - forcePointPosition.y;
            if (depth > 0f)
            {
                //Underwater
                //Apply buoyancy
                float displacementMultiplier = Mathf.Clamp01(depth / DepthBeforeSubmerged);

                float verticalBuoyancyForce = Volume * WaterDensity * Math.Abs(Physics.gravity.y) * displacementMultiplier / _pointCount;
                verticalBuoyancyForce = Mathf.Min(MaxBuoyancyForce, verticalBuoyancyForce);
                
                var buoyancyForce =  new Vector3(0, verticalBuoyancyForce, 0);
                _body.AddForceAtPosition(
                    buoyancyForce,
                    forcePointPosition,
                    ForceMode.Force);
                if(DrawForces) Debug.DrawLine(forcePointPosition, forcePointPosition+buoyancyForce, Color.blue, 0.1f);
            }
        }
    }
}