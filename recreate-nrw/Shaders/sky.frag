#version 460 core

in vec3 position;

out vec4 FragColor;

uniform vec3 sunDir;
uniform float sunFallOff;
uniform vec4 skyHorizon;
uniform vec4 skyZenith;

void main()
{
    vec3 normal = normalize(position);
    
    float altitude = normal.y;
    vec4 skyColor = mix(skyHorizon, skyZenith, max(altitude, 0.0));
            
    float distanceSun = dot(normal, sunDir) - 1.0;
    float sunIntensity = exp(sunFallOff * distanceSun);
    FragColor = mix(skyColor, vec4(1.0), sunIntensity);
}