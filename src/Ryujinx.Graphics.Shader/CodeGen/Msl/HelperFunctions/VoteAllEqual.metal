template<>
inline bool voteAllEqual(bool value)
{
    return simd_all(value) || !simd_any(value);
}
