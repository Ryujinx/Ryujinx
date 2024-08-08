template<typename T>
[[clang::optnone]] T PreciseFAdd(T l, T r) {
    return fma(T(1), l, r);
}

template<typename T>
[[clang::optnone]] T PreciseFSub(T l, T r) {
    return fma(T(-1), r, l);
}

template<typename T>
[[clang::optnone]] T PreciseFMul(T l, T r) {
    return fma(l, r, T(0));
}
