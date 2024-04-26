#version 460 core

in vec3 aPosition;

out vec3 position;

uniform mat4 viewMat;
uniform mat4 projectionMat;

void main()
{
    position = aPosition;
    vec4 final = vec4(aPosition, 1.0) * viewMat * projectionMat;
    gl_Position = final;
}