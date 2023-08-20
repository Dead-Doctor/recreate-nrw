#version 460 core

in vec3 position;
in vec3 normal;

out vec4 FragColor;

uniform vec3 lightDir;

uniform sampler2D concreteTexture;
float concreteToDirtStart = 0.78;
float concreteToDirtEnd = 0.8;
uniform sampler2D dirtTexture;
float dirtToGrassStart = 0.93;
float dirtToGrassEnd = 0.95;
uniform sampler2D grassTexture;

float ambientMax = 0.3;
float diffuseMax = 0.7;

float textureSize = 1.0;

vec4 mixColors(float value, vec4 colorA, float transitionABStart, float transitionABEnd, vec4 colorB, float transitionBCStart, float transitionBCEnd, vec4 colorC) {
    //TODO: remove branching
    if (value < transitionBCStart) return mix(colorA, colorB, smoothstep(transitionABStart, transitionABEnd, value));
    return mix(colorB, colorC, smoothstep(transitionBCStart, transitionBCEnd, value));
}

void main()
{
    float angle = dot(normal, vec3(0.0, 1.0, 0.0));
    vec2 uv = vec2(position.x, -position.z) / textureSize;
    
    vec4 concrete = texture(concreteTexture, uv);
    vec4 dirt = texture(dirtTexture, uv);
    vec4 grass = texture(grassTexture, uv);
    vec4 color = mixColors(angle, concrete, concreteToDirtStart, concreteToDirtEnd, dirt, dirtToGrassStart, dirtToGrassEnd, grass);
    
    float ambient = ambientMax;
    float diffuse = diffuseMax * max(dot(-lightDir, normal), 0.0);
    FragColor = (ambient + diffuse) * color;
}