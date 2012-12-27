// A tweaked version of ModuleRCS that fixes a few minor issues
// and adds some additional info to the module context menu.
// Much of the functionality is inherited from ModuleRCS.    
    
// Copyright (c) 2012, Oliver Jowett <oliver@mutability.co.uk>
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//
// Redistributions of source code must retain the above copyright notice, this
// list of conditions and the following disclaimer.
//
// Redistributions in binary form must reproduce the above copyright notice,
// this list of conditions and the following disclaimer in the documentation
// and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using UnityEngine;

namespace BetterRCS
{
    public class ModuleBetterRCS : ModuleRCS
    {
        // This data already exists in ModuleRCS, but it is private,
        // so we have to duplicate it here.
        private Vector3 inputRotate = Vector3.zero;     // Rotational inputs (pitch/yaw/roll) as a desired torque vector in worldspace
        private Vector3 inputTranslate = Vector3.zero;  // Translational inputs (RCS X/Y/Z) as a desired force vector in worldspace
        private int resourceHashcode;                   // Hashcode of the fuel resource name

        private static string STATUS_IDLE = "Idle";
        private static string STATUS_FIRING = "Firing";
        private static string STATUS_DEPRIVED = "Fuel deprived";
        private static string STATUS_DISABLED = "Disabled";
        
        // New context menu info
        
        [KSPField(isPersistant=false, guiName="Status", guiActive=true)]
        public string currentStatus = STATUS_DISABLED;
        [KSPField(isPersistant=false, guiName="Torque", guiUnits="kNm", guiFormat="F1", guiActive=true)]
        public float currentTorque = 0f;
        [KSPField(isPersistant=false, guiName="Thrust", guiUnits="kN", guiFormat="F1", guiActive=true)]
        public float currentThrust = 0f;
        [KSPField(isPersistant=false, guiName="Net force", guiUnits="kN", guiFormat="F1", guiActive=true)]
        public float currentForce = 0f;
        [KSPField(isPersistant=false, guiName="Fuel flow", guiUnits="U/s", guiFormat="F3", guiActive=true)]
        public float currentFuelFlow = 0f;
        
        public override void OnAwake() {
            base.OnAwake();
            this.resourceHashcode = base.resourceName.GetHashCode();
        }
        
        public new void Update() {
            Vessel vessel = base.part.vessel;
            if (vessel != null) {
                FlightCtrlState ctrl = vessel.ctrlState;
                
                // desired rotational torque
                this.inputRotate = vessel.ReferenceTransform.rotation * new Vector3(ctrl.pitch, ctrl.roll, ctrl.yaw);
                
                // desired translational force
                this.inputTranslate = vessel.ReferenceTransform.rotation * new Vector3(ctrl.X, ctrl.Z, ctrl.Y);
            }
        }
        
        public new void FixedUpdate()
        {
            // compute force per thruster            
            base.thrustForces.Clear();
            for (int i = 0; i < base.thrusterTransforms.Count; ++i) {
                base.thrustForces.Add(0f);
            }

            base.realISP = base.atmosphereCurve.Evaluate((float)base.vessel.staticPressure); // Isp in m/s                       
            this.currentStatus = STATUS_IDLE;
            
            if (!this.isEnabled ||
                (TimeWarp.CurrentRate > 1f && TimeWarp.WarpMode == TimeWarp.Modes.HIGH) ||
                !base.part.isControllable ||
                !base.vessel.ActionGroups[KSPActionGroup.RCS] ||
                (this.inputRotate == Vector3.zero && this.inputTranslate == Vector3.zero))
            {
                foreach (FXGroup fx in base.thrusterFX) {
                    fx.setActive(false);
                    fx.Power = 0f;
                }

                if (!this.isEnabled) {
                    currentStatus = STATUS_DISABLED;
                }
            
                this.currentFuelFlow = 0f;
                this.currentThrust = 0f;
                this.currentTorque = 0f;
                this.currentForce = 0f;
                return;                
            }
            
            float ispSpeed = base.realISP * base.G; // Isp in m/s (effective exhaust velocity)
            
            // Find the requested force at the thruster that produces the given torque.
            Vector3 effectiveCoM = (base.vessel.CoM + base.vessel.rb_velocity * Time.deltaTime);
            Vector3 appliedForce = Vector3.zero;
            Vector3 appliedTorque = Vector3.zero;
            this.currentFuelFlow = 0f;
            this.currentThrust = 0f;
            
            for (int i = 0; i < base.thrusterTransforms.Count; ++i)
            {
                Vector3 leverArm = base.thrusterTransforms[i].transform.position - effectiveCoM;
                Vector3 totalForce = this.inputTranslate + Vector3.Cross(this.inputRotate, leverArm.normalized);   // actually reversed

                // Find the thruster throttle for this particular sub-thruster.
                Vector3 thrustDirection = base.thrusterTransforms[i].up;
                // thrustDirection is opposite to the exerted force, but totalForce is similarly reversed so it all works out.
                float throttle = Mathf.Clamp01(Vector3.Dot(thrustDirection, totalForce));
                float scalarForce = throttle * base.thrusterPower;
                
                if (!base.isJustForShow && base.requiresFuel && !CheatOptions.InfiniteRCS && throttle > 0f) {
                    // Try to consume fuel.
                    //    F = ve * dm/dt   (dm/dt = mass flow rate)
                    // -> dm/dt = F / ve
                    
                    float fuelMass = scalarForce / ispSpeed * TimeWarp.deltaTime;
                    float fuelUnits = fuelMass / base.resourceMass;
                    float available = base.part.RequestResource(this.resourceHashcode, fuelUnits);
                    this.currentFuelFlow += available / TimeWarp.fixedDeltaTime;

                    if (available / fuelUnits < 0.10f) {
                        // Out of fuel, turn off thruster
                        throttle = scalarForce = 0f;
                        currentStatus = STATUS_DEPRIVED;                        
                    } else {
                        if (currentStatus == STATUS_IDLE)
                            currentStatus = STATUS_FIRING;
                    }
                }
                
                // Update thruster FX, apply thrust force
                if (throttle == 0f) {
                    base.thrusterFX[i].Power = 0f;                    
                    base.thrusterFX[i].setActive(false);
                } else {
                    base.thrusterFX[i].Power = Mathf.Clamp(throttle, 0.1f, 1.0f);
                    base.thrusterFX[i].setActive(true);
                    
                    if (!base.isJustForShow) {
                        Vector3 thrust = -thrustDirection * scalarForce;
                        base.part.rigidbody.AddForceAtPosition(-thrustDirection * scalarForce,
                                                               base.thrusterTransforms[i].position, ForceMode.Force);
                        appliedTorque += Vector3.Cross(thrust, leverArm);                        
                        appliedForce += thrust;
                        currentThrust += scalarForce;
                    }
                }
                
                // not used - just for compatibility with anything that expects the superclass values to be populated
                base.thrustForces[i] = throttle;
            }
            
            this.currentTorque = appliedTorque.magnitude;
            this.currentForce = appliedForce.magnitude;
        }
    }
}
