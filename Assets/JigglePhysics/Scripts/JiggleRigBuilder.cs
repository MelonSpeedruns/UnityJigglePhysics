using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JigglePhysics {

public class JiggleRigBuilder : MonoBehaviour {
    [System.Serializable]
    public class JiggleRig {
        [Tooltip("The root bone from which an individual JiggleRig will be constructed. The JiggleRig encompasses all children of the specified root.")]
        public Transform rootTransform;
        [Tooltip("The settings that the rig should update with, create them using the Create->JigglePhysics->Settings menu option.")]
        public JiggleSettingsBase jiggleSettings;
        [Tooltip("The list of transforms to ignore during the jiggle. Each bone listed will also ignore all the children of the specified bone.")]
        public List<Transform> ignoredTransforms;

        [HideInInspector]
        public List<JiggleBone> simulatedPoints;
    }
    [Tooltip("Enables interpolation for the simulation, this should be enabled unless you *really* need the simulation to only update on FixedUpdate.")]
    public bool interpolate = true;
    public List<JiggleRig> jiggleRigs;
    [Tooltip("An air force that is applied to the entire rig, this is useful to plug in some wind volumes from external sources.")]
    public Vector3 wind;
    [Tooltip("Draws some simple lines to show what the simulation is doing. Generally this should be disabled.")]
    [SerializeField] private bool debugDraw;

    private float accumulation;
    private bool shouldPop;
    private void Awake() {
        accumulation = 0f;
        foreach(JiggleRig rig in jiggleRigs) {
            rig.simulatedPoints = new List<JiggleBone>();
            CreateSimulatedPoints(rig, rig.rootTransform, null);
        }
    }
    private void LateUpdate() {
        if (!interpolate) {
            return;
        }

        foreach(JiggleRig rig in jiggleRigs) {
            foreach (JiggleBone simulatedPoint in rig.simulatedPoints) {
                if (shouldPop) {
                    simulatedPoint.PopAnimationPosition();
                }
                simulatedPoint.PrepareBone();
            }
        }
        shouldPop = false;

        accumulation = Mathf.Min(accumulation+Time.deltaTime, Time.fixedDeltaTime*4f);
        while (accumulation > Time.fixedDeltaTime) {
            accumulation -= Time.fixedDeltaTime;
            float time = Time.time - accumulation;
            foreach(JiggleRig rig in jiggleRigs) {
                foreach (JiggleBone simulatedPoint in rig.simulatedPoints) { 
                    simulatedPoint.Simulate(rig.jiggleSettings, wind, time);
                }
            }
        }

        foreach (JiggleRig rig in jiggleRigs) {
            foreach (JiggleBone simulatedPoint in rig.simulatedPoints) {
                simulatedPoint.DeriveFinalSolvePosition();
            }
        }

        foreach (JiggleRig rig in jiggleRigs) {
            foreach (JiggleBone simulatedPoint in rig.simulatedPoints) {
                simulatedPoint.PoseBone( rig.jiggleSettings.GetParameter(JiggleSettings.JiggleSettingParameter.Blend));
                if (debugDraw) {
                    simulatedPoint.DebugDraw(Color.red, Color.blue, true);
                }
            }
        }
    }

    private void FixedUpdate() {
        
        if (interpolate) {
            // Okay so this sucks, but its the only way I could think of it working. If FixedUpdate is called more than
            // once this frame, that would mean that LateUpdate would see it as a noticable "jump".
            // To prevent this, we try to feed updates up for n-1 FixedUpdates this frame...
            // Unity provides no way to know how many FixedUpdates we're going to be doing this frame, so
            // I simply *always* do n updates, then just "undo" the last position update during LateUpdate.
            foreach(JiggleRig rig in jiggleRigs) {
                foreach (JiggleBone simulatedPoint in rig.simulatedPoints) {
                    simulatedPoint.PrepareBone();
                }
            }
            shouldPop = true;
            return;
        }
        
        foreach(JiggleRig rig in jiggleRigs) {
            foreach (JiggleBone simulatedPoint in rig.simulatedPoints) {
                simulatedPoint.PrepareBone();
            }
        }

        foreach(JiggleRig rig in jiggleRigs) {
            foreach (JiggleBone simulatedPoint in rig.simulatedPoints) { 
                simulatedPoint.Simulate(rig.jiggleSettings, wind, Time.time);
            }
        }

        foreach (JiggleRig rig in jiggleRigs) {
            foreach (JiggleBone simulatedPoint in rig.simulatedPoints) {
                simulatedPoint.DeriveFinalSolvePosition();
            }
        }

        foreach (JiggleRig rig in jiggleRigs) {
            foreach (JiggleBone simulatedPoint in rig.simulatedPoints) {
                simulatedPoint.PoseBone( rig.jiggleSettings.GetParameter(JiggleSettings.JiggleSettingParameter.Blend));
                if (debugDraw) {
                    simulatedPoint.DebugDraw(Color.red, Color.blue, true);
                }
            }
        }
    }

    private void CreateSimulatedPoints(JiggleRig rig, Transform currentTransform, JiggleBone parentJiggleBone) {
        JiggleBone newJiggleBone = new JiggleBone(currentTransform, parentJiggleBone, currentTransform.position);
        rig.simulatedPoints.Add(newJiggleBone);
        // Create an extra purely virtual point if we have no children.
        if (currentTransform.childCount == 0) {
            if (newJiggleBone.parent == null) {
                if (newJiggleBone.transform.parent == null) {
                    throw new UnityException("Can't have a singular jiggle bone with no parents. That doesn't even make sense!");
                } else {
                    float lengthToParent = Vector3.Distance(currentTransform.position, newJiggleBone.transform.parent.position);
                    Vector3 projectedForwardReal = (currentTransform.position - newJiggleBone.transform.parent.position).normalized;
                    rig.simulatedPoints.Add(new JiggleBone(null, newJiggleBone, currentTransform.position + projectedForwardReal*lengthToParent));
                    return;
                }
            }
            Vector3 projectedForward = (currentTransform.position - parentJiggleBone.transform.position).normalized;
            float length = 0.1f;
            if (parentJiggleBone.parent != null) {
                length = Vector3.Distance(parentJiggleBone.transform.position, parentJiggleBone.parent.transform.position);
            }
            rig.simulatedPoints.Add(new JiggleBone(null, newJiggleBone, currentTransform.position + projectedForward*length));
            return;
        }
        for (int i = 0; i < currentTransform.childCount; i++) {
            if (rig.ignoredTransforms.Contains(currentTransform.GetChild(i))) {
                continue;
            }
            CreateSimulatedPoints(rig,currentTransform.GetChild(i), newJiggleBone);
        }
    }
}

}