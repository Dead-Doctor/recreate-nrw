#version 460 core

in vec2 aPosition;

out vec3 normal;

// Camera position + snap offset
uniform vec2 modelPos;
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

float getHeight(vec2 pos) {
    //TODO: multiple textures
    if (0.0 > pos.x || pos.x >= 2048.0 || 0.0 > pos.y || pos.y >= 2048.0) return 0.0;
    return texture(tile00, pos / 2048.0).r / 100.0;
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
    vec2 posOffsetDirection = vec2(and(posIsHorizontallyInside, leftSide * 2.0 - 1.0), and(posIsVerticallyInside, topSide * 2.0 - 1.0));
    
    float level = floor((indexWithinCorner - 1.0) / 3.0);
    float scale = pow(2.0, max(level, 0.0));
    
    vec2 offsetDirection = vec2(1.0 - leftSide * 2, 1.0 - topSide * 2);
    float offsetCount = floor(pow(2.0, level));
    
    // Shift chunk to correct corner
    vec2 relativPos = aPosition - n * vec2(leftSide, topSide);
    // Scale chunk
    relativPos *= scale;
    // Shift to correct ring
    relativPos += offsetCount * n * offsetDirection;
    // Shift to correct position within ring
    relativPos += scale * n * posOffsetDirection;
    
    vec2 pos = relativPos + modelPos;
    float height = getHeight(pos);
    
    //https://stackoverflow.com/questions/6656358/calculating-normals-in-a-triangle-mesh/21660173#21660173
    vec3 grid = vec3(1.0, 0.0, 1.0);
    float Zleft = getHeight(pos - grid.xy);
    float Zright = getHeight(pos + grid.xy);
    float Zup = getHeight(pos - grid.yz);
    float Zdown = getHeight(pos + grid.yz);
    float Zupleft = getHeight(pos - grid.xz);
    float Zdownright = getHeight(pos + grid.xz);
    normal = normalize(vec3( (2.0*(Zleft - Zright) - Zdownright + Zupleft + Zdown - Zup) / grid.x,
                             (2.0*(Zup - Zdown)    + Zdownright + Zupleft - Zdown - Zleft) / grid.z,
                             6.0 ));
    
    gl_Position = vec4(vec3(relativPos.x, height, relativPos.y), 1.0) * viewMat * projectionMat;
}