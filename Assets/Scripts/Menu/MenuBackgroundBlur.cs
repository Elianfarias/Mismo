using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mismo.Menu
{
    // Only the world camera is post-processed; IMGUI menus remain crisp.
    internal sealed class MenuBackgroundBlur
    {
        UniversalAdditionalCameraData cameraData;
        Camera targetCamera;
        Volume volume;
        VolumeProfile profile;
        DepthOfField blur;
        bool previousPostProcessing;
        CameraOverrideOption previousDepth;
        LayerMask previousVolumeMask;
        VolumeFrameworkUpdateMode previousVolumeUpdate;

        public void Update(bool enabled,float radius)
        {
            var camera=enabled?Camera.main:null;
            if(!enabled||camera!=targetCamera)Restore();
            if(!enabled||camera==null)return;
            if(cameraData==null)
            {
                cameraData=camera.GetUniversalAdditionalCameraData();
                targetCamera=camera;
                previousPostProcessing=cameraData.renderPostProcessing;
                previousDepth=cameraData.requiresDepthOption;
                previousVolumeMask=cameraData.volumeLayerMask;
                previousVolumeUpdate=camera.GetVolumeFrameworkUpdateMode();
                if(volume==null)
                {
                    var go=new GameObject("Menu background blur"){hideFlags=HideFlags.HideAndDontSave,layer=31};
                    volume=go.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10000;
                    profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.hideFlags=HideFlags.HideAndDontSave;
                    blur=profile.Add<DepthOfField>(true);
                    blur.mode.Override(DepthOfFieldMode.Gaussian);
                    blur.gaussianStart.Override(0);blur.gaussianEnd.Override(.1f);
                    blur.highQualitySampling.Override(true);
                    volume.sharedProfile=profile;
                }
                cameraData.renderPostProcessing=true;
                cameraData.requiresDepthOption=CameraOverrideOption.On;
                cameraData.volumeLayerMask=previousVolumeMask.value|(1<<31);
                camera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.EveryFrame);
            }
            blur.gaussianMaxRadius.Override(Mathf.Clamp(radius,.5f,1.5f));
            volume.weight=1;
        }
        void Restore()
        {
            if(volume!=null)volume.weight=0;
            if(cameraData!=null)
            {
                cameraData.renderPostProcessing=previousPostProcessing;
                cameraData.requiresDepthOption=previousDepth;
                cameraData.volumeLayerMask=previousVolumeMask;
                if(targetCamera!=null)targetCamera.SetVolumeFrameworkUpdateMode(previousVolumeUpdate);
            }
            cameraData=null;targetCamera=null;
        }
        public void Dispose()
        {
            Restore();
            if(volume!=null)Object.Destroy(volume.gameObject);
            if(blur!=null)Object.Destroy(blur);
            if(profile!=null)Object.Destroy(profile);
            volume=null;profile=null;blur=null;
        }
    }
}
