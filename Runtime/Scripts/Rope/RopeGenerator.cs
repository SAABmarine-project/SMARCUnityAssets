using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.AI;
using Utils = DefaultNamespace.Utils;

namespace Rope
{
    public class RopeGenerator : MonoBehaviour
    {
        [Header("Prefab of the rope parts")]
        public GameObject RopeLinkPrefab;

        [Header("Connected Body")]
        [Tooltip("What should the first link in the rope connect to?")]
        public string VehicleConnectionName;
    

        [Header("Rope parameters")]
        [Tooltip("Diameter of the rope in meters")]
        public float RopeDiameter = 0.01f;
        [Tooltip("How long the entire rope should be. Rounded to SegmentLength. Ignored if this is not the root of the rope.")]
        public float RopeLength = 1f;
        [Tooltip("How heavy is this rope?")]
        public float GramsPerMeter = 0.5f;
        [Tooltip("How heavy is the buoy at the end. Set to 0 for no buoy.")]
        public float BuoyGrams = 0f;

        [Header("Physics stuff")]
        [Tooltip("Diameter of the collision objects for the rope. The bigger the more stable the physics are.")]
        public float RopeCollisionDiameter = 0.1f;
        [Tooltip("How long each segment of the rope will be. Smaller = more realistic but harder to simulate.")]
        [Range(0.01f, 1f)]
        public float SegmentLength = 0.1f;
        [Tooltip("Mass of each segment compared to the base_link the rope is connected to. For physics stability! The larger the more stable...")]
        public float SegmentMassRatio = 0.01f;
        [Tooltip("Rope will be replaced by a stick when its end-to-end distance is >= than RopeLength*this")]
        [Range(0.9f, 1f)]
        public float RopeReplacementTolerance = 0.95f;


        [Header("Auto-calculated, no touchy in editor!")]
        public float SegmentRBMass = 1f;
        [Tooltip("This is the mass we'll use for gravity for each segment. In KGs")]
        public float IdealMassPerSegment => GramsPerMeter * 0.001f * SegmentLength;
        [Tooltip("All the rope links we generate will go in here.")]        
        public int NumSegments => (int)(RopeLength / (SegmentLength-RopeDiameter));
        public GameObject RopeContainer;


        GameObject vehicleBaseLinkConnection, baseLink;
        readonly string containerName = "Rope";
        readonly string baseLinkName = "base_link";

        void OnValidate()
        {
            if(NumSegments > 50) Debug.LogWarning($"There will be {NumSegments} rope segments generated on game Start, might be too many?");
        }

        GameObject InstantiateLink(Transform prevLink, int num, bool buoy)
        {
            var link = Instantiate(RopeLinkPrefab);
            link.transform.SetParent(RopeContainer.transform);
            link.name = $"{link.name}_{num}";
            if(buoy) link.name = $"{link.name}_buoy";

            var linkJoint = link.GetComponent<Joint>();
            if(prevLink != null)
            {
                var linkZ = prevLink.localPosition.z + (SegmentLength-RopeDiameter/2);
                link.transform.localPosition = new Vector3(0, 0, linkZ);
                link.transform.rotation = prevLink.rotation;
                linkJoint.connectedBody = prevLink.GetComponent<Rigidbody>();
            }
            else
            {
                // First link in the chain, not connected to another link
                // see what the parent has... and joint to it.
                if(vehicleBaseLinkConnection.TryGetComponent<ArticulationBody>(out ArticulationBody ab))
                    linkJoint.connectedArticulationBody = ab;
                if(vehicleBaseLinkConnection.TryGetComponent<Rigidbody>(out Rigidbody rb))
                    linkJoint.connectedBody = rb;

                link.transform.localPosition = new Vector3(0, 0, SegmentLength/2);
                link.transform.rotation = vehicleBaseLinkConnection.transform.rotation;

                // make the first link not collide with its attached base link
                if(baseLink.TryGetComponent<Collider>(out Collider baseCollider))
                {
                    var linkCollider = link.GetComponent<Collider>();
                    Physics.IgnoreCollision(linkCollider, baseCollider);
                }

            }

            var rl = link.GetComponent<RopeLink>();
            rl.SetRopeParams(this, buoy);
            return link;
        }


        public void SpawnRope()
        {
            vehicleBaseLinkConnection = Utils.FindDeepChildWithName(transform.root.gameObject, VehicleConnectionName);
            baseLink = Utils.FindDeepChildWithName(transform.root.gameObject, baseLinkName);

            if(RopeContainer == null)
            {
                RopeContainer = new GameObject(containerName);
                RopeContainer.transform.SetParent(transform.root);
                RopeContainer.transform.position = vehicleBaseLinkConnection.transform.position;
                RopeContainer.transform.rotation = vehicleBaseLinkConnection.transform.rotation;
            }
            
            // mass for each link so that the bodies can interact nicely
            // this mass wont be used for gravity!
            if(baseLink.TryGetComponent(out ArticulationBody BaseAB))
                SegmentRBMass = BaseAB.mass * SegmentMassRatio;
            if(baseLink.TryGetComponent(out Rigidbody BaseRB))
                SegmentRBMass = BaseRB.mass * SegmentMassRatio;

            SegmentRBMass = Mathf.Max(IdealMassPerSegment, SegmentRBMass);

            InstantiateLink(null, 0, false);

            for(int i=1; i < NumSegments; i++)
            {
                var buoy = (i+1 == NumSegments) && (BuoyGrams > 0);
                InstantiateLink(RopeContainer.transform.GetChild(i-1), i, buoy);
            }
        }

        public void DestroyRope()
        {
            while(true)
            {
                RopeContainer = Utils.FindDeepChildWithName(transform.root.gameObject, containerName);
                if(RopeContainer == null) break;
                DestroyImmediate(RopeContainer);
            }
        }

        public void ReplaceRopeWithStick(GameObject connectedHookGO)
        {
            // the rope breaking means its tight and carrying something.
            // so we replace the entire rope with a STICK
            // to make the physics more stable!
            // Reverse loop because we're gonna remove things from the collection
            var container = RopeContainer.transform;
            for(int i=container.childCount-1; i>=0; i--)
                Destroy(container.GetChild(i).gameObject);

            // Now that all the rope is gone, create a new RopeLink object
            // but, at this point, we shall have ONE segment that is as long as the rope
            SegmentLength = RopeLength;
            var stick = InstantiateLink(null, 0, false);
            // the default way these are instantiated does not fit this use case though
            // so we gotta orient and position this stick to match the current pose of
            // the hook connection and vehicle connection.
            // otherwise the joint will just throw both rapidly in different directions :D
            var connectionPoint = connectedHookGO.transform.Find("ConnectionPoint");
            var middlePos = (connectionPoint.position + vehicleBaseLinkConnection.transform.position)/2;
            stick.transform.position = middlePos;
            stick.transform.LookAt(connectionPoint);
            

            // this stick is already connected to the base_link
            // but now it also needs to connect to the hook's connection point
            var stickRopeLink = stick.GetComponent<RopeLink>();
            // we took the hook object from the ropelink that broke earlier.
            // since to break, it first had to attach, at which point it knew
            // the object to attach to.
            // See RopeLink::OnCollisionEnter then RopeLink::OnJointBreak
            stickRopeLink.ConnectToHook(connectedHookGO, breakable:false);
        }

        void Awake()
        {
            if(RopeContainer == null) RopeContainer = Utils.FindDeepChildWithName(transform.root.gameObject, containerName);
            if(vehicleBaseLinkConnection == null) vehicleBaseLinkConnection = Utils.FindDeepChildWithName(transform.root.gameObject, VehicleConnectionName);
            if(baseLink == null) baseLink = Utils.FindDeepChildWithName(transform.root.gameObject, baseLinkName);
        }

    }
}