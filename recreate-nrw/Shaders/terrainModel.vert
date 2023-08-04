#version 460 core

in vec3 aPosition;
in vec3 aNormal;

out vec3 position;
out vec3 normal;
out vec4 color;

uniform mat4 modelViewMat;
uniform mat4 projectionMat;

void main()
{
    position = aPosition;
    normal = aNormal;
    
    float min = 33.32;
    float max = 46.71;
    
    color = vec4(vec3((aPosition.y - min) / (max - min)), 1.0);

    gl_Position = vec4(aPosition, 1.0) * modelViewMat * projectionMat;
}