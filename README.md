# Precomputed Physically Based Atmosphere Scattering for Unity3D
This is an implementation of atmosphere scattering effect in Unity3D.  
The effect is physically based. Heavily used code from [1].  
This repo is currently really early-stage. Only skybox rendering is included. More features will be available later.

## Current features
Physically based calculation  
Support multi-scattering  
Runtime parameter update with little performance loss  

## Implementation details.


## Known issues 
Cloud can only be rendered behind objects. This is not resolved in the original presentation either.  

## TODO


## References
[1][Eric Bruneton. Precomputed Atmospheric Scattering: a New Implementation 2017](https://ebruneton.github.io/precomputed_atmospheric_scattering/)  
[2]‎Elek O. Rendering parametrizable planetary atmospheres with multiple scattering in real-time. 2009.
[3][Sebastien Hillaire. Physically Based Sky, Atmosphere and Cloud Rendering in Frostbite](https://blog.selfshadow.com/publications/s2016-shading-course/)  

## History.
2019/03/10 v0.1, Added README.md