#version 460 core

in vec3 aPosition;

out vec2 uv;
out float height;

uniform mat4 modelMat;
uniform mat4 viewMat;
uniform mat4 projectionMat;
uniform int n;

uniform isampler2D tile00;

float invert(float a)
{
    return 1.0 - a;
}

float notZero(float a)
{
    return min(a, 1.0);
}

float and(float a, float b)
{
    return a * b;
}

float xor(float a, float b)
{
    return mod(a + b, 2.0);
}

void main()
{
    float index = gl_InstanceID;
    float corner = mod(index, 4.0);
    float indexWithinCorner = floor(index / 4.0);
    
    float leftSide = abs(ceil(corner / 2.0) - 1.0);
    float topSide = invert(floor(corner / 2.0));
    
    float posIndex = mod(indexWithinCorner - 1.0, 3.0);
    float posInside = and(notZero(posIndex), notZero(indexWithinCorner));
    float posLeft = posIndex - posInside;
    float posRight = invert(posLeft);
    float posLeftIsHorizontallyInside = xor(leftSide, topSide);
    float posIsHorizontallyInside = and(posInside, xor(posRight, posLeftIsHorizontallyInside));
    float posIsVerticallyInside = and(posInside, xor(posLeft, posLeftIsHorizontallyInside));
    vec3 posOffsetDirection = vec3(and(posIsHorizontallyInside, leftSide * 2.0 - 1.0), 0.0, and(posIsVerticallyInside, topSide * 2.0 - 1.0));
    
    float level = floor((indexWithinCorner - 1.0) / 3.0);
    float scale = pow(2.0, max(level, 0.0));
    
    vec3 offsetDirection = vec3(1.0 - leftSide * 2, 0.0, 1.0 - topSide * 2);
    float offsetCount = floor(pow(2.0, level));
    
    // Shift chunk to correct corner
    vec3 position = aPosition - n * vec3(leftSide, 0.0, topSide);
    // Scale chunk
    position *= scale;
    // Shift to correct ring
    position += offsetCount * n * offsetDirection;
    // Shift to correct position within ring
    position += scale * n * posOffsetDirection;
    
    //TODO: Calculate normals
    
    uv = (vec4(position, 1.0) * modelMat).xz;
    
    height = texture(tile00, uv / 2048.0).r / 100.0;
    
    if (0.0 > uv.x || uv.x >= 2048.0 || 0.0 > uv.y || uv.y >= 2048.0) height = 0.0;
    
    gl_Position = vec4(position + vec3(0.0, height, 0.0), 1.0) * viewMat * projectionMat;
}