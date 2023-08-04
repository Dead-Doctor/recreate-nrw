#version 460 core
in vec3 aPosition;
in vec3 aNormal;
in vec2 aUV;

out vec3 position;
out vec3 normal;
out vec2 UV;

uniform mat4 modelViewMat;
uniform mat3 normalMat;
uniform mat4 projectionMat;

vec3 lightPos = vec3(-10.0, 10.0, 10.0);

void main()
{
    vec4 viewCoords = vec4(aPosition, 1.0) * modelViewMat;
    
    position = viewCoords.xyz;
    normal = aNormal * normalMat;
    UV = aUV;

    gl_Position = viewCoords * projectionMat;
}