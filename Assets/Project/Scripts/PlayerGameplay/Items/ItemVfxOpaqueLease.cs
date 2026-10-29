using UnityEngine;
using UnityEngine.Rendering.Universal;

// Restore the exact camera override when the final overlapping shockwave finishes.
public sealed class ItemVfxOpaqueLease : MonoBehaviour
{
    private UniversalAdditionalCameraData _data;
    private CameraOverrideOption _previous;
    private CameraOverrideOption _previousDepth;
    private float _until;
    private bool _owns;
    public void Request(UniversalAdditionalCameraData data,float duration)
    {
        if(!_owns){_data=data;_previous=data.requiresColorOption;_previousDepth=data.requiresDepthOption;_owns=true;}
        _data.requiresColorOption=CameraOverrideOption.On;
        _data.requiresDepthOption=CameraOverrideOption.On;
        _until=Mathf.Max(_until,Time.time+duration);
    }
    private void Update(){if(_owns&&Time.time>=_until){Restore();Destroy(this);}}
    private void Restore()
    {
        if(_owns&&_data)
        {
            if(_data.requiresColorOption==CameraOverrideOption.On)_data.requiresColorOption=_previous;
            if(_data.requiresDepthOption==CameraOverrideOption.On)_data.requiresDepthOption=_previousDepth;
        }
        _owns=false;
    }
    private void OnDisable()=>Restore();
}
