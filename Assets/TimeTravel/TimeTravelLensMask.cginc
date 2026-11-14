// The 3DS atlas already marks only the glass interior with non-opaque alpha.
float TimeTravelLensMask(float4 color)
{
    return 1-step(.99,color.a);
}
