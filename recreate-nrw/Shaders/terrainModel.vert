#version 460 core

in vec3 aPosition;
in vec3 aNormal;

out vec3 position;
out vec3 normal;

uniform mat4 modelViewMat;
uniform mat4 projectionMat;

void main()
{
    position = aPosition;
    normal = aNormal;

    gl_Position = vec4(aPosition, 1.0) * modelViewMat * projectionMat;
}