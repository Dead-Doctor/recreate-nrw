#version 460 core

in vec2 aPosition;

out vec2 pos;

// Camera position + snap offset
uniform vec2 modelPos;
uniform mat4 viewMat;
uniform mat4 projectionMat;
uniform int n;

//TODO: Upsample heightmap using bicubic interpolation (decrease size of triangles)

struct Tile
{
    vec2 pos;
    sampler2D data;
};

uniform Tile tiles[4];


float invert(float a)
{
    return 1.0 - a;
}

float notZero(float a)
{
    return min(a, 1.0);
}

float or(float a, float b)
{
    return min(a + b, 1.0);
}

float and(float a, float b)
{
    return a * b;
}

float xor(float a, float b)
{
    return mod(a + b, 2.0);
}

// Expects an integer
float getHeight(vec2 pos) {
    vec2 rounded = round(pos);
    vec2 offsetInTile = mod(mod(rounded, 2048.0) + 2048.0, 2048.0);
    vec2 fraction = offsetInTile / 2048.0;
    vec2 index = round((rounded - offsetInTile) / 2048.0);
    // Always sample textures to prevent mipmap errors at border
    float sample0 = texture(tiles[0].data, fraction).r;
    float sample1 = texture(tiles[1].data, fraction).r;
    float sample2 = texture(tiles[2].data, fraction).r;
    float sample3 = texture(tiles[3].data, fraction).r;
    float centimetres = tiles[0].pos == index ? sample0
                      : tiles[1].pos == index ? sample1
                      : tiles[2].pos == index ? sample2
                      : tiles[3].pos == index ? sample3
                      : 0.0;
    return centimetres / 100.0;
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
    pos = modelPos + relativPos;
    
    //Heights
    vec3 grid = vec3(scale, 0.0, scale);
    float yHere = getHeight(pos);
    float yLeft = getHeight(pos - grid.xy);
    float yRight = getHeight(pos + grid.xy);
    float yTop = getHeight(pos - grid.yz);
    float yBottom = getHeight(pos + grid.yz);
    
    //Fix seams
    vec2 edge = aPosition / n;

    float leftEdgeOfTile = mod(ceil(edge.x + 1.0), 2.0);
    float rightEdgeOfTile = floor(edge.x);
    float topEdgeOfTile = mod(ceil(edge.y + 1.0), 2.0);
    float bottomEdgeOfTile = floor(edge.y);

    float leftEdgeOfTileLeft = and(leftEdgeOfTile, leftSide);
    float rightEdgeOfTileRight = and(rightEdgeOfTile, invert(leftSide));
    float topEdgeOfTileTop = and(topEdgeOfTile, topSide);
    float bottomEdgeOfTileBottom = and(bottomEdgeOfTile, invert(topSide));

    float horizontalEdgeOfLevel = and(invert(posIsHorizontallyInside), or(leftEdgeOfTileLeft, rightEdgeOfTileRight));
    float verticalEdgeOfLevel = and(invert(posIsVerticallyInside), or(topEdgeOfTileTop, bottomEdgeOfTileBottom));

    float tileCouldHaveSeams = notZero(level + 1.0);
    float oddVertex = or(mod(aPosition.x, 2.0), mod(aPosition.y, 2.0));
    float vertexCouldHaveSeams = and(tileCouldHaveSeams, oddVertex);

    float horizontalSeam = and(horizontalEdgeOfLevel, vertexCouldHaveSeams);
    float verticalInterpolation = (yTop + yBottom) * 0.5;
    
    float verticalSeam = and(verticalEdgeOfLevel, vertexCouldHaveSeams);
    float horizontalInterpolation = (yLeft + yRight) * 0.5;

    float height = mix(mix(yHere, horizontalInterpolation, verticalSeam), verticalInterpolation, horizontalSeam);
    
    gl_Position = vec4(vec3(relativPos.x, height, relativPos.y), 1.0) * viewMat * projectionMat;
}