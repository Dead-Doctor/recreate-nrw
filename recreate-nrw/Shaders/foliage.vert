#version 460 core

// Vertex buffer
in vec3 aPosition;
in vec2 aUV;
// Instance array
uniform vec2 aOffset;
uniform float aRotation;

out vec2 uv;

uniform vec3 surfaceNormal;
uniform mat4 modelViewMat;
uniform mat4 projectionMat;

void main()
{
    uv = aUV;
    // front
    vec3 normal = vec3(cos(aRotation), 0.0, sin(aRotation));
    vec3 side = normalize(cross(surfaceNormal, normal));
    
    // rotated normal
    vec3 front = cross(surfaceNormal, side);
    
    mat3 mat = mat3(front, surfaceNormal, side);
    
    float height = 0.0;
    
    vec3 finalPosition = vec3(aOffset.x, height, aOffset.y) + mat * aPosition;

    gl_Position = vec4(finalPosition, 1.0) * modelViewMat * projectionMat;
}