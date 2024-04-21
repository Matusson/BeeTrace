# BeeTrace

BeeTrace is my path tracer intended to be used within Unity. Requires DirectX12 and RT core support. Designed for offline rendering (not realtime).

<br>
Please note that this is primarily a learning project for myself and is not intended for general use. I hope that this code can be useful for people learning about path tracing. If you need a ready-to-use offline path tracer, use Blender Cycles.
<br>

## Features
- PBR material support via Disney BSDF
- Volumetric scattering (fog and realistic SSS)
- Physically-based DOF
- ACES/AGX tonemappers
- Intel OpenImageDenoise support
- For Built-in RP


## Usage notes
- Tested for Unity 2022.3
- Create a new project with Built-in RP, install the package, then from the toolbar choose BeeTrace > Setup Scene. This will add the necessary components to the camera and add the BeeTrace manager to the scene.
- To see the path-traced image, enter play-mode. Note that Unity will revert your scene changes when you exit, so ensure that you make modifications outside of it.


## Acknowledgements
- [Scratchapixel](https://www.scratchapixel.com/) has been an incredible resource in learning about the basics of path tracing. If you're interested in learning this topic, it's a great starting point.
- [Joe Schutte's blog](https://schuttejoe.github.io/post/disneybsdf/) has been a great resource for implementing the Disney BSDF. It's a great explanation of the mathematics behind it.
- [TrueTrace](https://github.com/Pjbomb2/TrueTrace-Unity-Pathtracer) has also been very useful for the Disney BSDF implementation. It's a real-time path tracer, consider checking it out.

## License
This project is provided as-is under the MIT license.
