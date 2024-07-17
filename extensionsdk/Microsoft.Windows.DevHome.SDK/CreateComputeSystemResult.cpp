#include "pch.h"
#include "CreateComputeSystemResult.h"
#include "CreateComputeSystemResult.g.cpp"


namespace winrt::Microsoft::Windows::DevHome::SDK::implementation
{
    CreateComputeSystemResult::CreateComputeSystemResult(IComputeSystem const& computeSystem) :
        m_computeSystem(computeSystem), m_result(ProviderOperationStatus::Success, S_OK, hstring(), hstring())
    {
    }

    CreateComputeSystemResult::CreateComputeSystemResult(winrt::hresult const& e, hstring const& displayMessage, hstring const& diagnosticText) :
        m_computeSystem(nullptr), m_result(ProviderOperationStatus::Failure, e, displayMessage, diagnosticText)
    {
    }

    IComputeSystem CreateComputeSystemResult::ComputeSystem()
    {
        return m_computeSystem;
    }

    ProviderOperationResult CreateComputeSystemResult::Result()
    {
        return m_result;
    }
}
